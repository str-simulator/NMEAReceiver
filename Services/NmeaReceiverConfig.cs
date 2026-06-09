namespace NMEAReceiver.Services;

public sealed class NmeaReceiverConfig
{
    public int ComPortNo { get; set; } = 1;
    public int BaudRate { get; set; } = 38400;
    public List<(string Address, int Port)> UdpEndpoints { get; set; } = new() { ("127.0.0.1", 20011) };
}
