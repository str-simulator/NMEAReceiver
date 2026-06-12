using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;

namespace NMEAReceiver.Services;

public sealed class ReceiverChannelService : IReceiverChannelService
{
    private readonly Func<IWinRs232cReceiverService> _receiverFactory;
    private readonly Dictionary<string, IWinRs232cReceiverService> _receivers = new();
    private readonly HashSet<string> _allChannelNames = new();

    public event Action<string, int, IReadOnlyList<(string address, int port)>, string>? ChannelAdded;
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

    public void ConfigureChannel(string bindPortText, IReadOnlyList<(string address, int port)> udpDestinations)
    {
        if (!TryParseUdpBindPort(bindPortText, out var bindPort))
        {
            LogMessage?.Invoke($"Invalid UDP bind port: {bindPortText}");
            return;
        }

        var channelName = ToChannelName(bindPort);
        _allChannelNames.Add(channelName);
        ChannelAdded?.Invoke(channelName, bindPort, udpDestinations, "Stopped");
        RaiseStatusChanged();
    }

    public void OpenChannel(string bindPortText, IReadOnlyList<(string address, int port)> udpDestinations)
    {
        if (!TryParseUdpBindPort(bindPortText, out var bindPort))
        {
            LogMessage?.Invoke($"Invalid UDP bind port: {bindPortText}");
            return;
        }

        var channelName = ToChannelName(bindPort);
        if (_receivers.TryGetValue(channelName, out var stale))
        {
            stale.Dispose();
            _receivers.Remove(channelName);
        }

        var receiver = _receiverFactory();
        receiver.LogMessage += msg => LogMessage?.Invoke(msg);
        receiver.SentenceReceived += (name, sentence) => SentenceReceived?.Invoke(name, sentence);
        receiver.SentenceInfoUpdated += (name, data) => SentenceInfoUpdated?.Invoke(name, data);

        var config = new NmeaReceiverConfig
        {
            UdpBindPort = bindPort,
            UdpEndpoints = udpDestinations.Select(d => (d.address, d.port)).ToList(),
        };

        var opened = receiver.Open(config);
        if (opened)
            _receivers[channelName] = receiver;
        else
            receiver.Dispose();

        _allChannelNames.Add(channelName);
        ChannelAdded?.Invoke(channelName, bindPort, udpDestinations, opened ? "Running" : "Bind failed");

        if (!opened)
            LogMessage?.Invoke($"Failed to bind {channelName}");

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
        _allChannelNames.Remove(portName);
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
            var bindPort = ch.UdpBindPort > 0
                ? ch.UdpBindPort
                : TryParseUdpBindPort(ch.PortName, out var parsed) ? parsed : 0;

            if (bindPort <= 0)
                continue;

            var destinations = ch.UdpDestinations.Count > 0
                ? (IReadOnlyList<(string, int)>)ch.UdpDestinations.Select(d => (d.Address, d.Port)).ToList()
                : new[] { ("127.0.0.1", 20011) };

            if (ch.IsRunning)
            {
                OpenChannel(bindPort.ToString(), destinations);
            }
            else
            {
                ConfigureChannel(bindPort.ToString(), destinations);
            }
        }
    }

    private void RaiseStatusChanged()
        => StatusChanged?.Invoke(_receivers.Count, _allChannelNames.Count);

    public void Dispose()
    {
        foreach (var r in _receivers.Values) r.Dispose();
        _receivers.Clear();
        _allChannelNames.Clear();
    }

    private static string ToChannelName(int bindPort) => $"UDP:{bindPort}";

    private static bool TryParseUdpBindPort(string value, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith("UDP:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];

        return int.TryParse(normalized, out port) && port is > 0 and <= 65535;
    }
}
