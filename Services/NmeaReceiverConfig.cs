namespace NMEAReceiver.Services;

public sealed class NmeaReceiverConfig
{
    public int UdpBindPort { get; set; } = 40014;
    public List<(string Address, int Port)> UdpEndpoints { get; set; } = new() { ("127.0.0.1", 20011) };
}
