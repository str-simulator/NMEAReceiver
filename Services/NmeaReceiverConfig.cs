namespace NMEAReceiver.Services;

public enum ReceiverInputMode
{
    Serial,
    Udp
}

public sealed class NmeaReceiverConfig
{
    public ReceiverInputMode InputMode { get; set; } = ReceiverInputMode.Serial;
    public int ComPortNo { get; set; } = 1;
    public int BaudRate { get; set; } = 38400;
    public int UdpBindPort { get; set; } = 40014;
    public List<(string Address, int Port)> UdpEndpoints { get; set; } = new() { ("127.0.0.1", 20011) };
}
