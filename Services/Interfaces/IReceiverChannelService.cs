using NMEAReceiver.ViewModels;

namespace NMEAReceiver.Services.Interfaces;

public interface IReceiverChannelService : IDisposable
{
    void OpenChannel(string comPort, int baudRate, string udpAddress, int udpPort);
    void StopChannel(ChannelRowViewModel channel);
    void DeleteChannel(ChannelRowViewModel channel);
    void RestoreChannels(IEnumerable<IniChannelSettings> channels, IReadOnlyCollection<string> availablePorts);
}
