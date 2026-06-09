namespace NMEAReceiver.Services;

public sealed class IniSettings
{
    public string SelectedComPort { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 38400;
    public string DefaultUdpAddress { get; set; } = "127.0.0.1";
    public int DefaultUdpPort { get; set; } = 20011;
    public List<IniChannelSettings> Channels { get; } = new();
}

public sealed class IniChannelSettings
{
    public string PortName { get; set; } = string.Empty;
    public int BaudRate { get; set; } = 38400;
    public List<(string Address, int Port)> UdpDestinations { get; } = new();
    public bool IsRunning { get; set; } = true;
}
