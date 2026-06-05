using NMEAReceiver.Services;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels.Panels;

namespace NMEAReceiver.ViewModels.Shell;

public sealed class MainViewModel : IDisposable
{
    private readonly MainStateStore _store;
    private readonly IReceiverChannelService _channelService;
    private readonly IIniPersistenceService _iniService;

    public ChannelSetupViewModel ChannelSetup { get; }
    public ChannelsTableViewModel ChannelsTable { get; }
    public SnapshotViewModel Snapshot { get; }
    public DiagnosticViewModel Diagnostic { get; }

    public MainViewModel(
        MainStateStore store,
        ChannelSetupViewModel channelSetup,
        ChannelsTableViewModel channelsTable,
        SnapshotViewModel snapshot,
        DiagnosticViewModel diagnostic,
        IReceiverChannelService channelService,
        IIniPersistenceService iniService)
    {
        _store = store;
        _channelService = channelService;
        _iniService = iniService;
        ChannelSetup = channelSetup;
        ChannelsTable = channelsTable;
        Snapshot = snapshot;
        Diagnostic = diagnostic;

        var saved = iniService.Load();
        if (saved.BaudRate > 0) channelSetup.BaudRate = saved.BaudRate;
        if (!string.IsNullOrWhiteSpace(saved.DefaultUdpAddress)) channelSetup.DefaultUdpAddress = saved.DefaultUdpAddress;
        if (saved.DefaultUdpPort > 0) channelSetup.DefaultUdpPort = saved.DefaultUdpPort;
        if (!string.IsNullOrWhiteSpace(saved.SelectedComPort) && store.AvailableComPorts.Contains(saved.SelectedComPort))
            channelSetup.SelectedComPort = saved.SelectedComPort;

        channelService.RestoreChannels(saved.Channels, store.AvailableComPorts);
    }

    public void Dispose()
    {
        var settings = new IniSettings
        {
            SelectedComPort = ChannelSetup.SelectedComPort ?? string.Empty,
            BaudRate = ChannelSetup.BaudRate,
            DefaultUdpAddress = ChannelSetup.DefaultUdpAddress,
            DefaultUdpPort = ChannelSetup.DefaultUdpPort,
        };
        foreach (var c in _store.Channels)
            settings.Channels.Add(new IniChannelSettings
            {
                PortName = c.PortName,
                BaudRate = c.BaudRate,
                UdpAddress = c.UdpAddress,
                UdpPort = c.UdpPort,
                IsRunning = c.IsRunning,
            });
        _iniService.Save(settings);
        _channelService.Dispose();
    }
}
