using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels;
using NMEAReceiver.ViewModels.Shell;
using System.ComponentModel;
using System.Text;
using System.Windows;

namespace NMEAReceiver.Services;

public sealed class ReceiverChannelService : IReceiverChannelService
{
    private readonly Dictionary<string, IWinRs232cReceiverService> _receivers = new();
    private readonly MainStateStore _store;
    private readonly Func<IWinRs232cReceiverService> _receiverFactory;

    public ReceiverChannelService(MainStateStore store, Func<IWinRs232cReceiverService> receiverFactory)
    {
        _store = store;
        _receiverFactory = receiverFactory;
    }

    public void OpenChannel(string comPort, int baudRate, IReadOnlyList<(string address, int port)> udpDestinations)
    {
        var existing = _store.Channels.FirstOrDefault(c => c.PortName == comPort);
        if (existing is not null)
        {
            if (_receivers.TryGetValue(comPort, out var stale)) { stale.Dispose(); _receivers.Remove(comPort); }
            existing.PropertyChanged -= OnChannelPropertyChanged;
            existing.UdpDestinations.CollectionChanged -= OnChannelUdpCollectionChanged;
            _store.Channels.Remove(existing);
        }

        if (!int.TryParse(comPort.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
        {
            _store.AppendLog($"Invalid port name: {comPort}");
            return;
        }

        var channel = new ChannelRowViewModel(portNo, udpDestinations, baudRate);
        WireChannelEvents(channel);
        _store.Channels.Add(channel);

        var config = new NmeaReceiverConfig
        {
            ComPortNo = portNo,
            BaudRate = baudRate,
            UdpEndpoints = udpDestinations.Select(d => (d.address, d.port)).ToList(),
        };

        var receiver = _receiverFactory();
        receiver.LogMessage += _store.AppendLog;
        receiver.SentenceReceived += OnSentenceReceived;
        receiver.SentenceInfoUpdated += OnSentenceInfoUpdated;

        var opened = receiver.Open(config);
        channel.IsRunning = opened;
        channel.Status = opened ? "Running" : "Open failed";
        _receivers[comPort] = receiver;

        UpdateStatus();
    }

    public void StopChannel(ChannelRowViewModel channel)
    {
        if (_receivers.TryGetValue(channel.PortName, out var receiver))
        {
            receiver.Dispose();
            _receivers.Remove(channel.PortName);
        }
        channel.IsRunning = false;
        channel.Status = "Stopped";
        UpdateStatus();
    }

    public void DeleteChannel(ChannelRowViewModel channel)
    {
        if (_receivers.TryGetValue(channel.PortName, out var receiver))
        {
            receiver.Dispose();
            _receivers.Remove(channel.PortName);
        }
        channel.PropertyChanged -= OnChannelPropertyChanged;
        channel.UdpDestinations.CollectionChanged -= OnChannelUdpCollectionChanged;
        _store.Channels.Remove(channel);
        UpdateStatus();
    }

    public void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts)
    {
        foreach (var ch in channels)
        {
            if (!availablePorts.Contains(ch.PortName))
                continue;

            var destinations = ch.UdpDestinations.Count > 0
                ? ch.UdpDestinations
                : new List<(string, int)> { ("127.0.0.1", 20011) };

            if (ch.IsRunning)
            {
                OpenChannel(ch.PortName, ch.BaudRate, destinations);
            }
            else
            {
                if (!int.TryParse(ch.PortName.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
                    continue;
                var row = new ChannelRowViewModel(portNo, destinations, ch.BaudRate)
                {
                    IsRunning = false,
                    Status = "Stopped",
                };
                WireChannelEvents(row);
                _store.Channels.Add(row);
                UpdateStatus();
            }
        }
    }

    private void WireChannelEvents(ChannelRowViewModel channel)
    {
        channel.PropertyChanged += OnChannelPropertyChanged;
        channel.UdpDestinations.CollectionChanged += OnChannelUdpCollectionChanged;
        foreach (var dest in channel.UdpDestinations)
            dest.PropertyChanged += (_, _) => OnChannelUdpDestinationChanged(channel);
    }

    private void UpdateStatus()
    {
        var open = _receivers.Values.Count(r => r.IsOpen());
        _store.IsRunning = open > 0;
        _store.StatusText = open > 0
            ? $"Running  {open} / {_store.Channels.Count} channel(s)"
            : "Stopped";
    }

    private void OnSentenceReceived(string channelName, string sentence)
    {
        var ch = _store.Channels.FirstOrDefault(c => c.PortName == channelName);
        ch?.AppendRawLog(sentence);
    }

    private void OnSentenceInfoUpdated(string channelName, ST_IOSSEND_SENTENCE data)
    {
        Application.Current.Dispatcher.Invoke(() =>
            _store.SentenceSnapshot = BuildSnapshot(channelName, data));
    }

    private static string BuildSnapshot(string channelName, ST_IOSSEND_SENTENCE s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Last Channel : {channelName}");
        sb.AppendLine($"HTD Override : {DisplayChar(s.m_stSentenceHTD.szOverride)}");
        sb.AppendLine($"HTD RudderAngle : {s.m_stSentenceHTD.dRudderAngle}");
        sb.AppendLine($"HTD SteeringMode : {s.m_stSentenceHTD.nSteeringMode}");
        sb.AppendLine($"RSA STBD Sensor : {s.m_stSentenceRSA.dStarboardRudderSensor}");
        sb.AppendLine($"RSA PORT Sensor : {s.m_stSentenceRSA.dPortRudderSensor}");
        sb.AppendLine($"ROR STBD Order : {s.m_stSentenceROR.dStarboardRudderOrder}");
        sb.AppendLine($"ROR PORT Order : {s.m_stSentenceROR.dPortRudderOrder}");
        sb.AppendLine($"ROR Source : {DisplayChar(s.m_stSentenceROR.szCommandedSourceLocation)}");
        sb.AppendLine($"PYDKN Mode : {s.m_stSentencePYDKN.nSteeringMode}");
        sb.AppendLine($"ALF Id : {s.m_stSentenceALF.dAlertIdentifier}");
        sb.AppendLine($"ALC Id : {s.m_stSentenceALC.dAlertIdentifier}");
        sb.AppendLine($"ARC Command : {DisplayChar(s.m_stSentenceARC.szAlertCommand)}");
        sb.AppendLine($"ACN Command : {DisplayChar(s.m_stSentenceACN.szAlertCommand)}");
        sb.AppendLine($"HBT Interval : {s.m_stSentenceHBT.nConfiguredRepeatInterval}");
        sb.AppendLine($"HBT Status : {DisplayChar(s.m_stSentenceHBT.szEquipmentStatus)}");
        return sb.ToString();
    }

    private void OnChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ChannelRowViewModel channel) return;
        if (e.PropertyName != nameof(ChannelRowViewModel.UdpDestinations)) return;
        if (!channel.IsRunning) return;

        UpdateChannelUdpEndpoints(channel);
    }

    private void OnChannelUdpCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        var channel = _store.Channels.FirstOrDefault(c => c.UdpDestinations == sender);
        if (channel is null || !channel.IsRunning) return;

        if (e.NewItems is not null)
            foreach (UdpDestinationViewModel dest in e.NewItems)
                dest.PropertyChanged += (_, _) => OnChannelUdpDestinationChanged(channel);

        UpdateChannelUdpEndpoints(channel);
    }

    private void OnChannelUdpDestinationChanged(ChannelRowViewModel channel)
    {
        if (!channel.IsRunning) return;
        UpdateChannelUdpEndpoints(channel);
    }

    private void UpdateChannelUdpEndpoints(ChannelRowViewModel channel)
    {
        if (!_receivers.TryGetValue(channel.PortName, out var receiver)) return;

        var endpoints = channel.UdpDestinations.Select(d => (d.Address, d.Port));
        receiver.UpdateUdpEndpoints(endpoints);
        _store.AppendLog($"{channel.PortName} UDP destinations updated → {channel.UdpDestinationsSummary}");
    }

    private static string DisplayChar(char c) => c == '\0' ? "(null)" : c.ToString();

    public void Dispose()
    {
        foreach (var r in _receivers.Values) r.Dispose();
        _receivers.Clear();
    }
}
