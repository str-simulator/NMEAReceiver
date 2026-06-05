using CommunityToolkit.Mvvm.ComponentModel;

namespace NMEAReceiver.ViewModels;

public partial class ChannelRowViewModel : ObservableObject
{
    [ObservableProperty] private int portNo;
    [ObservableProperty] private string portName = string.Empty;
    [ObservableProperty] private string status = "Stopped";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private int baudRate = 38400;
    [ObservableProperty] private string udpAddress = "127.0.0.1";
    [ObservableProperty] private int udpPort = 20011;
    [ObservableProperty] private string lastSentence = string.Empty;
    [ObservableProperty] private string lastUpdated = string.Empty;

    public ChannelRowViewModel(int portNo, string udpAddress = "127.0.0.1", int udpPort = 20011, int baudRate = 38400)
    {
        PortNo = portNo;
        PortName = $"COM{portNo}";
        UdpAddress = udpAddress;
        UdpPort = udpPort;
        BaudRate = baudRate;
    }
}
