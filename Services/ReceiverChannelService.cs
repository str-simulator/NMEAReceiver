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
    private readonly Dictionary<string, WinRs232cReceiverService> _receivers = new();
    private readonly MainStateStore _store;

    public ReceiverChannelService(MainStateStore store) => _store = store;

    public void OpenChannel(string comPort, int baudRate, string udpAddress, int udpPort)
    {
        var existing = _store.Channels.FirstOrDefault(c => c.PortName == comPort);
        if (existing is not null)
        {
            if (_receivers.TryGetValue(comPort, out var stale)) { stale.Dispose(); _receivers.Remove(comPort); }
            existing.PropertyChanged -= OnChannelPropertyChanged;
            _store.Channels.Remove(existing);
        }

        if (!int.TryParse(comPort.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
        {
            _store.AppendLog($"Invalid port name: {comPort}");
            return;
        }

        var channel = new ChannelRowViewModel(portNo, udpAddress, udpPort, baudRate);
        channel.PropertyChanged += OnChannelPropertyChanged;
        _store.Channels.Add(channel);

        var config = new NmeaReceiverConfig
        {
            ComPortNo = portNo,
            BaudRate = baudRate,
            IosSendPortNo = udpPort,
            IosSendAddress = string.IsNullOrWhiteSpace(udpAddress) ? "127.0.0.1" : udpAddress.Trim(),
        };

        var receiver = new WinRs232cReceiverService();
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
        _store.Channels.Remove(channel);
        UpdateStatus();
    }

    public void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts)
    {
        foreach (var ch in channels)
        {
            if (!availablePorts.Contains(ch.PortName))
                continue;

            if (ch.IsRunning)
            {
                OpenChannel(ch.PortName, ch.BaudRate, ch.UdpAddress, ch.UdpPort);
            }
            else
            {
                if (!int.TryParse(ch.PortName.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
                    continue;
                var row = new ChannelRowViewModel(portNo, ch.UdpAddress, ch.UdpPort, ch.BaudRate)
                {
                    IsRunning = false,
                    Status = "Stopped",
                };
                row.PropertyChanged += OnChannelPropertyChanged;
                _store.Channels.Add(row);
                UpdateStatus();
            }
        }
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
        Application.Current.Dispatcher.Invoke(() =>
        {
            _store.RawSentence = sentence;
            var ch = _store.Channels.FirstOrDefault(c => c.PortName == channelName);
            if (ch is not null)
            {
                ch.LastSentence = sentence;
                ch.LastUpdated = DateTime.Now.ToString("HH:mm:ss.fff");
            }
        });
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
        if (e.PropertyName != nameof(ChannelRowViewModel.UdpAddress) &&
            e.PropertyName != nameof(ChannelRowViewModel.UdpPort)) return;
        if (!channel.IsRunning) return;

        ReconnectChannel(channel);
    }

    private void ReconnectChannel(ChannelRowViewModel channel)
    {
        if (_receivers.TryGetValue(channel.PortName, out var old))
        {
            old.Dispose();
            _receivers.Remove(channel.PortName);
        }

        _store.AppendLog($"{channel.PortName} UDP changed → {channel.UdpAddress}:{channel.UdpPort}, reconnecting...");

        if (!int.TryParse(channel.PortName.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
            return;

        var config = new NmeaReceiverConfig
        {
            ComPortNo = portNo,
            BaudRate = channel.BaudRate,
            IosSendPortNo = channel.UdpPort,
            IosSendAddress = string.IsNullOrWhiteSpace(channel.UdpAddress) ? "127.0.0.1" : channel.UdpAddress.Trim(),
        };

        var receiver = new WinRs232cReceiverService();
        receiver.LogMessage += _store.AppendLog;
        receiver.SentenceReceived += OnSentenceReceived;
        receiver.SentenceInfoUpdated += OnSentenceInfoUpdated;

        var opened = receiver.Open(config);
        channel.IsRunning = opened;
        channel.Status = opened ? "Running" : "Open failed";
        _receivers[channel.PortName] = receiver;

        UpdateStatus();
    }

    private static string DisplayChar(char c) => c == '\0' ? "(null)" : c.ToString();

    public void Dispose()
    {
        foreach (var r in _receivers.Values) r.Dispose();
        _receivers.Clear();
    }
}
