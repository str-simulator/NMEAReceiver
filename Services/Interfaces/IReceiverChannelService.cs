using NMEAReceiver.ViewModels;

namespace NMEAReceiver.Services.Interfaces;

public interface IReceiverChannelService : IDisposable
{
    void OpenChannel(string comPort, int baudRate, IReadOnlyList<(string address, int port)> udpDestinations);
    void StopChannel(ChannelRowViewModel channel);
    void DeleteChannel(ChannelRowViewModel channel);
    void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts);
}
