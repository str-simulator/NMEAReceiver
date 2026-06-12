using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface IReceiverChannelService : IDisposable
{
    // (channelName, bindPort, udpDestinations, status: "Running"/"Stopped"/"Bind failed")
    event Action<string, int, IReadOnlyList<(string address, int port)>, string>? ChannelAdded;
    event Action<string>? ChannelStopped;
    event Action<string>? ChannelDeleted;
    event Action<string, string>? SentenceReceived;
    event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;
    event Action<string>? LogMessage;
    // (openCount, totalCount)
    event Action<int, int>? StatusChanged;

    void ConfigureChannel(string bindPortText, IReadOnlyList<(string address, int port)> udpDestinations);
    void OpenChannel(string bindPortText, IReadOnlyList<(string address, int port)> udpDestinations);
    void StopChannel(string portName);
    void DeleteChannel(string portName);
    void UpdateChannelUdpEndpoints(string portName, IEnumerable<(string address, int port)> endpoints);
    void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts);
}
