namespace NMEAReceiver.Services;

public sealed class NmeaReceiverConfig
{
    public int ComPortNo { get; set; } = 1;
    public int BaudRate { get; set; } = 38400;

    public int IosSendPortNo { get; set; } = 20011;
    public string IosSendAddress { get; set; } = "127.0.0.1";

    public static NmeaReceiverConfig Load(string iniPath, string configName)
    {
        return new NmeaReceiverConfig();
    }

    public void Save(string iniPath, string configName)
    {
    }

    public NmeaReceiverConfig CloneForPort(int portNo)
    {
        return new NmeaReceiverConfig
        {
            ComPortNo = portNo,
            BaudRate = BaudRate,
            IosSendPortNo = IosSendPortNo,
            IosSendAddress = IosSendAddress,
        };
    }

    public static IReadOnlyList<int> LoadPortList(string iniPath, string configName)
    {
        return new[] { 1 };
    }

    public static void SavePortList(string iniPath, IEnumerable<int> ports)
    {
    }
}
