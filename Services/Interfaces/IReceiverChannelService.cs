using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface IReceiverChannelService : IDisposable
{
    // (portName, portNo, baudRate, udpDestinations, status: "Running"/"Stopped"/"Open failed")
    event Action<string, int, int, IReadOnlyList<(string address, int port)>, string>? ChannelAdded;
    event Action<string>? ChannelStopped;
    event Action<string>? ChannelDeleted;
    event Action<string, string>? SentenceReceived;
    event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;
    event Action<string>? LogMessage;
    // (openCount, totalCount)
    event Action<int, int>? StatusChanged;

    void OpenChannel(string comPort, int baudRate, IReadOnlyList<(string address, int port)> udpDestinations);
    void StopChannel(string portName);
    void DeleteChannel(string portName);
    void UpdateChannelUdpEndpoints(string portName, IEnumerable<(string address, int port)> endpoints);
    void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts);
}
