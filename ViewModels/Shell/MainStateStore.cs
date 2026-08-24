using CommunityToolkit.Mvvm.ComponentModel;
using NMEAReceiver.Models;
using NMEAReceiver.Services;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace NMEAReceiver.ViewModels.Shell;

public sealed partial class MainStateStore : ObservableObject
{
    private const int MaxLogLength = 200_000;
    private static readonly TimeSpan FlushInterval = TimeSpan.FromMilliseconds(150);

    private readonly IReceiverChannelService _channelService;
    private readonly DispatcherTimer _flushTimer;

    private readonly object _pendingLock = new();
    private readonly StringBuilder _pendingLog = new();
    private readonly Dictionary<string, StringBuilder> _pendingRawLog = new();
    private (string PortName, ST_IOSSEND_SENTENCE Data)? _pendingSnapshot;

    public ObservableCollection<ChannelRowViewModel> Channels { get; } = new();
    public ObservableCollection<string> AvailableComPorts { get; } = new();

    [ObservableProperty] private ChannelRowViewModel? selectedChannel;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string statusText = "Status: Stopped";
    [ObservableProperty] private string sentenceSnapshot = string.Empty;
    [ObservableProperty] private string logText = string.Empty;

    public MainStateStore(IReceiverChannelService channelService)
    {
        _channelService = channelService;
        channelService.ChannelAdded += OnChannelAdded;
        channelService.ChannelStopped += OnChannelStopped;
        channelService.ChannelDeleted += OnChannelDeleted;
        channelService.SentenceReceived += OnSentenceReceived;
        channelService.SentenceInfoUpdated += OnSentenceInfoUpdated;
        channelService.LogMessage += AppendLog;
        channelService.StatusChanged += OnStatusChanged;

        _flushTimer = new DispatcherTimer { Interval = FlushInterval };
        _flushTimer.Tick += (_, _) => FlushPending();
        _flushTimer.Start();
    }

    // 시리얼/UDP 수신 스레드에서 호출됨 — 반드시 가볍게 유지하고 Dispatcher를 건드리지 않아야 한다.
    // 그렇지 않으면 빠른 데이터 소스가 UI 스레드를 앞질러서 처리되지 않은 작업이 무한히 쌓인다.
    public void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        lock (_pendingLock)
            _pendingLog.Append(line);
    }

    private void FlushPending()
    {
        string? pendingLog = null;
        Dictionary<string, StringBuilder>? pendingRawLog = null;
        (string PortName, ST_IOSSEND_SENTENCE Data)? pendingSnapshot = null;

        lock (_pendingLock)
        {
            if (_pendingLog.Length > 0)
            {
                pendingLog = _pendingLog.ToString();
                _pendingLog.Clear();
            }

            if (_pendingRawLog.Count > 0)
            {
                pendingRawLog = new Dictionary<string, StringBuilder>(_pendingRawLog);
                _pendingRawLog.Clear();
            }

            pendingSnapshot = _pendingSnapshot;
            _pendingSnapshot = null;
        }

        if (pendingLog is not null)
        {
            var text = LogText + pendingLog;
            LogText = text.Length > MaxLogLength ? text[^MaxLogLength..] : text;
        }

        if (pendingRawLog is not null)
        {
            foreach (var (portName, block) in pendingRawLog)
                Channels.FirstOrDefault(c => c.PortName == portName)?.AppendRawLogBlock(block.ToString());
        }

        if (pendingSnapshot is { } snapshot)
            SentenceSnapshot = BuildSnapshot(snapshot.PortName, snapshot.Data);
    }

    private void OnChannelAdded(string portName, int portNo, int baudRate,
        IReadOnlyList<(string address, int port)> udpDestinations, string status)
    {
        Dispatch(() =>
        {
            var existing = Channels.FirstOrDefault(c => c.PortName == portName);
            if (existing is not null)
                Channels.Remove(existing);

            var inputMode = portName.StartsWith("UDP:", StringComparison.OrdinalIgnoreCase)
                ? ReceiverInputMode.Udp
                : ReceiverInputMode.Serial;

            var channel = new ChannelRowViewModel(portName, portNo, inputMode, udpDestinations, baudRate)
            {
                IsRunning = status == "Running",
                Status = status,
            };
            WireChannelEvents(channel);
            Channels.Add(channel);
            SelectedChannel = channel;
        });
    }

    private void OnChannelStopped(string portName)
    {
        Dispatch(() =>
        {
            var channel = Channels.FirstOrDefault(c => c.PortName == portName);
            if (channel is null) return;
            channel.IsRunning = false;
            channel.Status = "Stopped";
        });
    }

    private void OnChannelDeleted(string portName)
    {
        Dispatch(() =>
        {
            var channel = Channels.FirstOrDefault(c => c.PortName == portName);
            if (channel is null) return;
            Channels.Remove(channel);
        });
    }

    // 아래 두 핸들러는 시리얼/UDP 수신 스레드에서 NMEA 센텐스 수신 속도로 호출되는데,
    // 이는 WPF가 렌더링할 수 있는 속도를 훨씬 넘어설 수 있다. 이벤트마다 매번 디스패치하는
    // 대신, 여기서는 버퍼에만 쌓아두고 FlushPending()이 일정 주기로 UI 스레드에서 한꺼번에 반영한다.
    private void OnSentenceReceived(string portName, string sentence)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {sentence}{Environment.NewLine}";
        lock (_pendingLock)
        {
            if (!_pendingRawLog.TryGetValue(portName, out var buffer))
            {
                buffer = new StringBuilder();
                _pendingRawLog[portName] = buffer;
            }
            buffer.Append(line);
        }
    }

    private void OnSentenceInfoUpdated(string portName, ST_IOSSEND_SENTENCE data)
    {
        lock (_pendingLock)
            _pendingSnapshot = (portName, data);
    }

    private void OnStatusChanged(int openCount, int totalCount)
    {
        Dispatch(() =>
        {
            IsRunning = openCount > 0;
            StatusText = openCount > 0
                ? $"Running  {openCount} / {totalCount} channel(s)"
                : "Stopped";
        });
    }

    private void WireChannelEvents(ChannelRowViewModel channel)
    {
        channel.UdpDestinations.CollectionChanged += (_, e) =>
        {
            if (e.NewItems is not null)
                foreach (UdpDestinationViewModel dest in e.NewItems)
                    dest.PropertyChanged += (_, _) => OnChannelUdpChanged(channel);
            OnChannelUdpChanged(channel);
        };
        foreach (var dest in channel.UdpDestinations)
            dest.PropertyChanged += (_, _) => OnChannelUdpChanged(channel);
    }

    private void OnChannelUdpChanged(ChannelRowViewModel channel)
    {
        if (!channel.IsRunning) return;
        var endpoints = channel.UdpDestinations.Select(d => (d.Address, d.Port));
        _channelService.UpdateChannelUdpEndpoints(channel.PortName, endpoints);
        AppendLog($"{channel.PortName} UDP destinations updated -> {channel.UdpDestinationsSummary}");
    }

    private static void Dispatch(Action action)
    {
        if (Application.Current.Dispatcher.CheckAccess())
            action();
        else
            Application.Current.Dispatcher.Invoke(action);
    }

    private static string BuildSnapshot(string channelName, ST_IOSSEND_SENTENCE s)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Last Channel : {channelName}");
        sb.AppendLine($"HTD Override : {DisplayChar(s.m_stSentenceHTD.szOverride)}");
        sb.AppendLine($"HTD RudderAngle : {s.m_stSentenceHTD.dRudderAngle}");
        sb.AppendLine($"HTD SteeringMode : {s.m_stSentenceHTD.nSteeringMode}");
        sb.AppendLine($"RSA STBD Sensor : {s.m_stSentenceRSA.dStarboardRudderSensor}");
        sb.AppendLine($"RSA PORT Sensor : {s.m_stSentenceRSA.dPortRudderSensor}");
        sb.AppendLine($"ROR STBD Order : {s.m_stSentenceROR.dStarboardRudderOrder}");
        sb.AppendLine($"ROR PORT Order : {s.m_stSentenceROR.dPortRudderOrder}");
        sb.AppendLine($"ROR Source : {DisplayChar(s.m_stSentenceROR.szCommandedSourceLocation)}");
        sb.AppendLine($"PYDKN Mode : {s.m_stSentencePYDKN.nSteeringMode}");
        sb.AppendLine($"ALF Id : {s.m_stSentenceALF.dAlertIdentifier}");
        sb.AppendLine($"ALC Id : {s.m_stSentenceALC.dAlertIdentifier}");
        sb.AppendLine($"ARC Command : {DisplayChar(s.m_stSentenceARC.szAlertCommand)}");
        sb.AppendLine($"ACN Command : {DisplayChar(s.m_stSentenceACN.szAlertCommand)}");
        sb.AppendLine($"HBT Interval : {s.m_stSentenceHBT.nConfiguredRepeatInterval}");
        sb.AppendLine($"HBT Status : {DisplayChar(s.m_stSentenceHBT.szEquipmentStatus)}");
        return sb.ToString();
    }

    private static string DisplayChar(char c) => c == '\0' ? "(null)" : c.ToString();
}
