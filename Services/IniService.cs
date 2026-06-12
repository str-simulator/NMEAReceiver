using NMEAReceiver.Services.Interfaces;
using System.IO;
using System.Reflection;

namespace NMEAReceiver.Services;

public sealed class IniService : IIniPersistenceService
{
    private readonly string _path;

    public IniService()
    {
        var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? AppContext.BaseDirectory;
        _path = Path.Combine(dir, "NMEAReceiver.ini");
    }

    public void Save(IniSettings settings)
    {
        var lines = new List<string>
        {
            "[Settings]",
            $"SelectedInputMode={settings.SelectedInputMode}",
            $"SelectedComPort={settings.SelectedComPort}",
            $"BaudRate={settings.BaudRate}",
            $"UdpBindPort={settings.UdpBindPort}",
            $"DefaultUdpAddress={settings.DefaultUdpAddress}",
            $"DefaultUdpPort={settings.DefaultUdpPort}",
            $"Channels={string.Join(",", settings.Channels.Select(c => c.PortName))}",
        };

        foreach (var ch in settings.Channels)
        {
            lines.Add(string.Empty);
            lines.Add($"[Channel.{ch.PortName}]");
            lines.Add($"InputMode={ch.InputMode}");
            lines.Add($"BaudRate={ch.BaudRate}");
            lines.Add($"UdpBindPort={ch.UdpBindPort}");
            for (int i = 0; i < ch.UdpDestinations.Count; i++)
                lines.Add($"Udp{i}={ch.UdpDestinations[i].Address}:{ch.UdpDestinations[i].Port}");
            lines.Add($"IsRunning={ch.IsRunning}");
        }

        File.WriteAllLines(_path, lines);
    }

    public IniSettings Load()
    {
        var result = new IniSettings();
        if (!File.Exists(_path))
            return result;

        string? currentSection = null;
        IniChannelSettings? currentChannel = null;

        foreach (var line in File.ReadLines(_path))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith(';') || string.IsNullOrEmpty(trimmed))
                continue;

            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                currentSection = trimmed[1..^1];
                if (currentSection.StartsWith("Channel.", StringComparison.OrdinalIgnoreCase))
                {
                    currentChannel = new IniChannelSettings { PortName = currentSection[8..] };
                    result.Channels.Add(currentChannel);
                }
                else
                {
                    currentChannel = null;
                }
                continue;
            }

            if (!trimmed.Contains('='))
                continue;

            var sep = trimmed.IndexOf('=');
            var key = trimmed[..sep].Trim();
            var value = trimmed[(sep + 1)..].Trim();

            if (currentSection == "Settings")
            {
                switch (key)
                {
                    case "SelectedInputMode":
                        if (Enum.TryParse<ReceiverInputMode>(value, true, out var mode)) result.SelectedInputMode = mode;
                        break;
                    case "SelectedComPort": result.SelectedComPort = value; break;
                    case "BaudRate": if (int.TryParse(value, out var b)) result.BaudRate = b; break;
                    case "UdpBindPort": if (int.TryParse(value, out var ubp)) result.UdpBindPort = ubp; break;
                    case "DefaultUdpAddress": result.DefaultUdpAddress = value; break;
                    case "DefaultUdpPort": if (int.TryParse(value, out var p)) result.DefaultUdpPort = p; break;
                }
            }
            else if (currentChannel is not null)
            {
                switch (key)
                {
                    case "InputMode":
                        if (Enum.TryParse<ReceiverInputMode>(value, true, out var mode)) currentChannel.InputMode = mode;
                        break;
                    case "BaudRate":
                        if (int.TryParse(value, out var b)) currentChannel.BaudRate = b;
                        break;
                    case "UdpBindPort":
                        if (int.TryParse(value, out var ubp)) currentChannel.UdpBindPort = ubp;
                        break;
                    case "IsRunning":
                        currentChannel.IsRunning = !value.Equals("false", StringComparison.OrdinalIgnoreCase);
                        break;
                    default:
                        if (key.StartsWith("Udp", StringComparison.OrdinalIgnoreCase) && key.Length > 3
                            && int.TryParse(key[3..], out _))
                        {
                            var colonIdx = value.LastIndexOf(':');
                            if (colonIdx > 0 && int.TryParse(value[(colonIdx + 1)..], out var udpPort))
                                currentChannel.UdpDestinations.Add((value[..colonIdx], udpPort));
                        }
                        break;
                }
            }
        }

        foreach (var ch in result.Channels)
        {
            if (ch.PortName.StartsWith("UDP:", StringComparison.OrdinalIgnoreCase))
                ch.InputMode = ReceiverInputMode.Udp;

            if (ch.UdpBindPort <= 0 && TryParseUdpBindPort(ch.PortName, out var bindPort))
                ch.UdpBindPort = bindPort;

            if (ch.UdpDestinations.Count == 0)
                ch.UdpDestinations.Add(("127.0.0.1", 20011));
        }

        return result;
    }

    private static bool TryParseUdpBindPort(string value, out int port)
    {
        port = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = value.Trim();
        if (normalized.StartsWith("UDP:", StringComparison.OrdinalIgnoreCase))
            normalized = normalized[4..];

        return int.TryParse(normalized, out port) && port is > 0 and <= 65535;
    }
}
