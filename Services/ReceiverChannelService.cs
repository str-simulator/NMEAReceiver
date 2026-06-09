using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;

namespace NMEAReceiver.Services;

public sealed class ReceiverChannelService : IReceiverChannelService
{
    private readonly Func<IWinRs232cReceiverService> _receiverFactory;
    private readonly Dictionary<string, IWinRs232cReceiverService> _receivers = new();
    private readonly HashSet<string> _allPortNames = new();

    public event Action<string, int, int, IReadOnlyList<(string address, int port)>, string>? ChannelAdded;
    public event Action<string>? ChannelStopped;
    public event Action<string>? ChannelDeleted;
    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;
    public event Action<string>? LogMessage;
    public event Action<int, int>? StatusChanged;

    public ReceiverChannelService(Func<IWinRs232cReceiverService> receiverFactory)
    {
        _receiverFactory = receiverFactory;
    }

    public void OpenChannel(string comPort, int baudRate, IReadOnlyList<(string address, int port)> udpDestinations)
    {
        if (!int.TryParse(comPort.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
        {
            LogMessage?.Invoke($"Invalid port name: {comPort}");
            return;
        }

        if (_receivers.TryGetValue(comPort, out var stale))
        {
            stale.Dispose();
            _receivers.Remove(comPort);
        }

        var receiver = _receiverFactory();
        receiver.LogMessage += msg => LogMessage?.Invoke(msg);
        receiver.SentenceReceived += (name, sentence) => SentenceReceived?.Invoke(name, sentence);
        receiver.SentenceInfoUpdated += (name, data) => SentenceInfoUpdated?.Invoke(name, data);

        var config = new NmeaReceiverConfig
        {
            ComPortNo = portNo,
            BaudRate = baudRate,
            UdpEndpoints = udpDestinations.Select(d => (d.address, d.port)).ToList(),
        };

        var opened = receiver.Open(config);
        if (opened)
            _receivers[comPort] = receiver;
        else
            receiver.Dispose();

        _allPortNames.Add(comPort);
        ChannelAdded?.Invoke(comPort, portNo, baudRate, udpDestinations, opened ? "Running" : "Open failed");

        if (!opened)
            LogMessage?.Invoke($"Failed to open {comPort}");

        RaiseStatusChanged();
    }

    public void StopChannel(string portName)
    {
        if (_receivers.TryGetValue(portName, out var receiver))
        {
            receiver.Dispose();
            _receivers.Remove(portName);
        }
        ChannelStopped?.Invoke(portName);
        RaiseStatusChanged();
    }

    public void DeleteChannel(string portName)
    {
        if (_receivers.TryGetValue(portName, out var receiver))
        {
            receiver.Dispose();
            _receivers.Remove(portName);
        }
        _allPortNames.Remove(portName);
        ChannelDeleted?.Invoke(portName);
        RaiseStatusChanged();
    }

    public void UpdateChannelUdpEndpoints(string portName, IEnumerable<(string address, int port)> endpoints)
    {
        if (_receivers.TryGetValue(portName, out var receiver))
            receiver.UpdateUdpEndpoints(endpoints);
    }

    public void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts)
    {
        foreach (var ch in channels)
        {
            if (!availablePorts.Contains(ch.PortName))
                continue;

            var destinations = ch.UdpDestinations.Count > 0
                ? (IReadOnlyList<(string, int)>)ch.UdpDestinations.Select(d => (d.Address, d.Port)).ToList()
                : new[] { ("127.0.0.1", 20011) };

            if (ch.IsRunning)
            {
                OpenChannel(ch.PortName, ch.BaudRate, destinations);
            }
            else
            {
                if (!int.TryParse(ch.PortName.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
                    continue;

                _allPortNames.Add(ch.PortName);
                ChannelAdded?.Invoke(ch.PortName, portNo, ch.BaudRate, destinations, "Stopped");
                RaiseStatusChanged();
            }
        }
    }

    private void RaiseStatusChanged()
        => StatusChanged?.Invoke(_receivers.Count, _allPortNames.Count);

    public void Dispose()
    {
        foreach (var r in _receivers.Values) r.Dispose();
        _receivers.Clear();
        _allPortNames.Clear();
    }
}
