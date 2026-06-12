namespace NMEAReceiver.Services;

public sealed class IniSettings
{
    public ReceiverInputMode SelectedInputMode { get; set; } = ReceiverInputMode.Serial;
    public string SelectedComPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 38400;
    public int UdpBindPort { get; set; } = 40014;
    public string DefaultUdpAddress { get; set; } = "127.0.0.1";
    public int DefaultUdpPort { get; set; } = 20011;
    public List<IniChannelSettings> Channels { get; } = new();
}

public sealed class IniChannelSettings
{
    public ReceiverInputMode InputMode { get; set; } = ReceiverInputMode.Serial;
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 38400;
    public int UdpBindPort { get; set; }
    public List<(string Address, int Port)> UdpDestinations { get; } = new();
    public bool IsRunning { get; set; } = true;
}
