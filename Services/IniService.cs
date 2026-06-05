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
            $"SelectedComPort={settings.SelectedComPort}",
            $"BaudRate={settings.BaudRate}",
            $"DefaultUdpAddress={settings.DefaultUdpAddress}",
            $"DefaultUdpPort={settings.DefaultUdpPort}",
            $"Channels={string.Join(",", settings.Channels.Select(c => c.PortName))}",
        };

        foreach (var ch in settings.Channels)
        {
            lines.Add(string.Empty);
            lines.Add($"[Channel.{ch.PortName}]");
            lines.Add($"BaudRate={ch.BaudRate}");
            lines.Add($"UdpAddress={ch.UdpAddress}");
            lines.Add($"UdpPort={ch.UdpPort}");
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
                    case "SelectedComPort": result.SelectedComPort = value; break;
                    case "BaudRate": if (int.TryParse(value, out var b)) result.BaudRate = b; break;
                    case "DefaultUdpAddress": result.DefaultUdpAddress = value; break;
                    case "DefaultUdpPort": if (int.TryParse(value, out var p)) result.DefaultUdpPort = p; break;
                }
            }
            else if (currentChannel is not null)
            {
                switch (key)
                {
                    case "BaudRate": if (int.TryParse(value, out var b)) currentChannel.BaudRate = b; break;
                    case "UdpAddress": currentChannel.UdpAddress = value; break;
                    case "UdpPort": if (int.TryParse(value, out var p)) currentChannel.UdpPort = p; break;
                    case "IsRunning": currentChannel.IsRunning = !value.Equals("false", StringComparison.OrdinalIgnoreCase); break;
                }
            }
        }

        return result;
    }
}

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
    public string UdpAddress { get; set; } = "127.0.0.1";
    public int UdpPort { get; set; } = 20011;
    public bool IsRunning { get; set; } = true;
}
