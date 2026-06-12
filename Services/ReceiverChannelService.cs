using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;

namespace NMEAReceiver.Services;

public sealed class ReceiverChannelService : IReceiverChannelService
{
    private readonly Func<IWinRs232cReceiverService> _receiverFactory;
    private readonly Dictionary<string, IWinRs232cReceiverService> _receivers = new();
    private readonly HashSet<string> _allChannelNames = new();

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

    public void ConfigureChannel(NmeaReceiverConfig config)
    {
        if (!TryNormalizeConfig(config, out var normalized, out var channelName, out var portNo, out var error))
        {
            LogMessage?.Invoke(error);
            return;
        }

        _allChannelNames.Add(channelName);
        ChannelAdded?.Invoke(channelName, portNo, normalized.BaudRate, normalized.UdpEndpoints, "Stopped");
        RaiseStatusChanged();
    }

    public void OpenChannel(NmeaReceiverConfig config)
    {
        if (!TryNormalizeConfig(config, out var normalized, out var channelName, out var portNo, out var error))
        {
            LogMessage?.Invoke(error);
            return;
        }

        if (_receivers.TryGetValue(channelName, out var stale))
        {
            stale.Dispose();
            _receivers.Remove(channelName);
        }

        var receiver = _receiverFactory();
        receiver.LogMessage += msg => LogMessage?.Invoke(msg);
        receiver.SentenceReceived += (name, sentence) => SentenceReceived?.Invoke(name, sentence);
        receiver.SentenceInfoUpdated += (name, data) => SentenceInfoUpdated?.Invoke(name, data);

        var opened = receiver.Open(normalized);
        if (opened)
            _receivers[channelName] = receiver;
        else
            receiver.Dispose();

        _allChannelNames.Add(channelName);
        var failStatus = normalized.InputMode == ReceiverInputMode.Udp ? "Bind failed" : "Open failed";
        ChannelAdded?.Invoke(channelName, portNo, normalized.BaudRate, normalized.UdpEndpoints, opened ? "Running" : failStatus);

        if (!opened)
            LogMessage?.Invoke(normalized.InputMode == ReceiverInputMode.Udp
                ? $"Failed to bind {channelName}"
                : $"Failed to open {channelName}");

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
            var inputMode = ch.InputMode;
            if (inputMode == ReceiverInputMode.Serial && ch.PortName.StartsWith("UDP:", StringComparison.OrdinalIgnoreCase))
                inputMode = ReceiverInputMode.Udp;

            var config = new NmeaReceiverConfig
            {
                InputMode = inputMode,
                ComPortNo = TryParseComPortNo(ch.PortName, out var comPortNo) ? comPortNo : 0,
                UdpBindPort = ch.UdpBindPort > 0
                    ? ch.UdpBindPort
                    : TryParseUdpBindPort(ch.PortName, out var bindPort) ? bindPort : 0,
                BaudRate = ch.BaudRate,
                UdpEndpoints = ch.UdpDestinations.Count > 0
                    ? ch.UdpDestinations.Select(d => (d.Address, d.Port)).ToList()
                    : new() { ("127.0.0.1", 20011) },
            };

            if (config.InputMode == ReceiverInputMode.Serial && !availablePorts.Contains(ch.PortName))
                continue;

            if (ch.IsRunning)
                OpenChannel(config);
            else
                ConfigureChannel(config);
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

    private static bool TryNormalizeConfig(
        NmeaReceiverConfig config,
        out NmeaReceiverConfig normalized,
        out string channelName,
        out int portNo,
        out string error)
    {
        normalized = new NmeaReceiverConfig
        {
            InputMode = config.InputMode,
            ComPortNo = config.ComPortNo,
            UdpBindPort = config.UdpBindPort,
            BaudRate = config.BaudRate,
            UdpEndpoints = config.UdpEndpoints.ToList(),
        };
        channelName = string.Empty;
        portNo = 0;
        error = string.Empty;

        if (normalized.UdpEndpoints.Count == 0)
            normalized.UdpEndpoints.Add(("127.0.0.1", 20011));

        if (normalized.InputMode == ReceiverInputMode.Udp)
        {
            if (normalized.UdpBindPort is <= 0 or > 65535)
            {
                error = $"Invalid UDP bind port: {normalized.UdpBindPort}";
                return false;
            }

            channelName = $"UDP:{normalized.UdpBindPort}";
            portNo = normalized.UdpBindPort;
            normalized.ComPortNo = 0;
            normalized.BaudRate = 0;
            return true;
        }

        if (normalized.ComPortNo <= 0)
        {
            error = "Invalid COM port.";
            return false;
        }

        channelName = $"COM{normalized.ComPortNo}";
        portNo = normalized.ComPortNo;
        return true;
    }

    private static bool TryParseComPortNo(string value, out int portNo)
    {
        portNo = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith("COM", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[3..];

        return int.TryParse(normalized, out portNo) && portNo > 0;
    }

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
