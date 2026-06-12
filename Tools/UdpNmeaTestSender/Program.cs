using System.Net;
using System.Net.Sockets;
using System.Text;

var options = SenderOptions.Parse(args);
if (options.ShowHelp)
{
    PrintHelp();
    return 0;
}

using var udp = new UdpClient();
var endpoint = new IPEndPoint(IPAddress.Parse(options.Host), options.Port);
var sentences = BuildSentences(options.IncludeChecksum);

Console.WriteLine($"UDP NMEA test sender -> {endpoint}");
Console.WriteLine($"Sentences: {sentences.Count}, Interval: {options.IntervalMs} ms, Loop: {options.Loop}");
Console.WriteLine("Press Ctrl+C to stop.");
Console.WriteLine();

var stop = false;
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    stop = true;
};

do
{
    foreach (var sentence in sentences)
    {
        if (stop)
            break;

        var payload = Encoding.ASCII.GetBytes(sentence + "\r\n");
        await udp.SendAsync(payload, payload.Length, endpoint);
        Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} -> {sentence}");

        if (options.IntervalMs > 0)
            await Task.Delay(options.IntervalMs);
    }
} while (options.Loop && !stop);

return 0;

static List<string> BuildSentences(bool includeChecksum)
{
    var bodies = new[]
    {
        // Parsed by Sentence.HTD.
        "IIHTD,A,12.3,R,S,T,5.0,1.0,30.0,2.5,045.5,0.2,045.5,T,A,A,A,046.0",

        // Parsed by Sentence.RSA.
        "IIRSA,3.4,A,-2.1,A",

        // Parsed by Sentence.ROR.
        "IIROR,1.5,A,-1.5,A,L",

        // Parsed by Sentence.PYDKN.
        "PYDKN,N,A,S,P",

        // Parsed by Sentence.ALF.
        "IIALF,1,1,100,120000,A,W,A,M,101,1,0,0,T",

        // Parsed by Sentence.ALC.
        "IIALC,1,1,100,2,M,101,1,0,A,E",

        // Parsed by Sentence.ARC.
        "IIARC,120000,M,A,1,A",

        // Parsed by Sentence.ACN.
        "IIACN,120000,M,101,1,A,A",

        // Parsed by Sentence.HBT.
        "IIHBT,5,A,1",

        // Current receiver code routes GGA to HBT branch.
        "GPGGA,123519,3500.000,N,12900.000,E,1,08,0.9,10.0,M,0.0,M,,",
    };

    return bodies
        .Select(body => includeChecksum ? $"${body}*{Checksum(body):X2}" : $"${body}")
        .ToList();
}

static int Checksum(string body)
{
    var checksum = 0;
    foreach (var ch in body)
        checksum ^= ch;
    return checksum;
}

static void PrintHelp()
{
    Console.WriteLine("""
UDP NMEA Test Sender

Usage:
  dotnet run --project Tools/UdpNmeaTestSender -- [options]

Options:
  --host <ip>       Destination IP address. Default: 127.0.0.1
  --port <number>   Destination UDP port. Default: 40014
  --interval <ms>   Delay between sentences. Default: 500
  --loop            Repeat until Ctrl+C.
  --checksum        Append NMEA checksum.
  --help            Show help.

Examples:
  dotnet run --project Tools/UdpNmeaTestSender -- --port 40014
  dotnet run --project Tools/UdpNmeaTestSender -- --host 127.0.0.1 --port 40014 --loop --interval 1000

Note:
  The receiver parser currently does not strip checksum text before parsing fields.
  The default payload therefore omits checksum so every implemented numeric field is parsed cleanly.
""");
}

internal sealed class SenderOptions
{
    public string Host { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 40014;
    public int IntervalMs { get; init; } = 500;
    public bool Loop { get; init; }
    public bool IncludeChecksum { get; init; }
    public bool ShowHelp { get; init; }

    public static SenderOptions Parse(string[] args)
    {
        var host = "127.0.0.1";
        var port = 40014;
        var intervalMs = 500;
        var loop = false;
        var checksum = false;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                    host = ReadValue(args, ref i, "--host");
                    break;
                case "--port":
                    port = int.Parse(ReadValue(args, ref i, "--port"));
                    break;
                case "--interval":
                    intervalMs = int.Parse(ReadValue(args, ref i, "--interval"));
                    break;
                case "--loop":
                    loop = true;
                    break;
                case "--checksum":
                    checksum = true;
                    break;
                case "--help":
                case "-h":
                case "/?":
                    help = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown option: {args[i]}");
            }
        }

        if (!IPAddress.TryParse(host, out _))
            throw new ArgumentException($"Invalid host IP address: {host}");

        if (port is <= 0 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(port), "UDP port must be 1-65535.");

        if (intervalMs < 0)
            throw new ArgumentOutOfRangeException(nameof(intervalMs), "Interval must be 0 or greater.");

        return new SenderOptions
        {
            Host = host,
            Port = port,
            IntervalMs = intervalMs,
            Loop = loop,
            IncludeChecksum = checksum,
            ShowHelp = help,
        };
    }

    private static string ReadValue(string[] args, ref int index, string option)
    {
        if (index + 1 >= args.Length)
            throw new ArgumentException($"{option} requires a value.");

        index++;
        return args[index];
    }
}
