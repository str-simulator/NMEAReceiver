using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface IReceiverChannelService : IDisposable
{
    // (channelName, portNoOrBindPort, baudRate, udpDestinations, status: "Running"/"Stopped"/"Open failed"/"Bind failed")
    event Action<string, int, int, IReadOnlyList<(string address, int port)>, string>? ChannelAdded;
    event Action<string>? ChannelStopped;
    event Action<string>? ChannelDeleted;
    event Action<string, string>? SentenceReceived;
    event Action<string, IReadOnlyList<(Sentence Type, ST_IOSSEND_SENTENCE Data)>>? SentenceInfoUpdated;
    event Action<string, TtmTargetData>? TtmTargetUpdated;
    event Action<string>? LogMessage;
    // (openCount, totalCount)
    event Action<int, int>? StatusChanged;

    void ConfigureChannel(NmeaReceiverConfig config);
    void OpenChannel(NmeaReceiverConfig config);
    void StopChannel(string portName);
    void DeleteChannel(string portName);
    void UpdateChannelUdpEndpoints(string portName, IEnumerable<(string address, int port)> endpoints);
    void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts);
}
