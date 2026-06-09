using CommunityToolkit.Mvvm.ComponentModel;
using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Text;
using System.Windows;

namespace NMEAReceiver.ViewModels.Shell;

public sealed partial class MainStateStore : ObservableObject
{
    private readonly IReceiverChannelService _channelService;

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
    }

    public void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        Dispatch(() => LogText += line);
    }

    private void OnChannelAdded(string portName, int portNo, int baudRate,
        IReadOnlyList<(string address, int port)> udpDestinations, string status)
    {
        Dispatch(() =>
        {
            var existing = Channels.FirstOrDefault(c => c.PortName == portName);
            if (existing is not null)
                Channels.Remove(existing);

            var channel = new ChannelRowViewModel(portNo, udpDestinations, baudRate)
            {
                IsRunning = status == "Running",
                Status = status,
            };
            WireChannelEvents(channel);
            Channels.Add(channel);
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

    private void OnSentenceReceived(string portName, string sentence)
    {
        var channel = Channels.FirstOrDefault(c => c.PortName == portName);
        channel?.AppendRawLog(sentence);
    }

    private void OnSentenceInfoUpdated(string portName, ST_IOSSEND_SENTENCE data)
    {
        Dispatch(() => SentenceSnapshot = BuildSnapshot(portName, data));
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
        AppendLog($"{channel.PortName} UDP destinations updated → {channel.UdpDestinationsSummary}");
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
