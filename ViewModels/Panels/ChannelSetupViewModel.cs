using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class ChannelSetupViewModel : ObservableObject
{
    private readonly MainStateStore _store;
    private readonly IReceiverChannelService _channelService;
    private readonly IComPortService _comPortService;

    [ObservableProperty] private string? selectedComPort;
    [ObservableProperty] private int baudRate = 38400;
    [ObservableProperty] private string defaultUdpAddress = "127.0.0.1";
    [ObservableProperty] private int defaultUdpPort = 20011;

    public ObservableCollection<string> AvailableComPorts => _store.AvailableComPorts;
    public string StatusText => _store.StatusText;

    public static IReadOnlyList<int> AvailableBaudRates { get; } = new[]
    {
        1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200, 230400, 460800, 921600
    };

    public ChannelSetupViewModel(
        MainStateStore store,
        IReceiverChannelService channelService,
        IComPortService comPortService)
    {
        _store = store;
        _channelService = channelService;
        _comPortService = comPortService;

        _store.PropertyChanged += OnStorePropertyChanged;
        RefreshComPorts();
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.StatusText))
            OnPropertyChanged(nameof(StatusText));

        if (e.PropertyName == nameof(MainStateStore.SelectedChannel) && _store.SelectedChannel is { } ch)
        {
            SelectedComPort = _store.AvailableComPorts.Contains(ch.PortName) ? ch.PortName : SelectedComPort;
            BaudRate = ch.BaudRate;

            var first = ch.UdpDestinations.FirstOrDefault();
            if (first is not null)
            {
                DefaultUdpAddress = first.Address;
                DefaultUdpPort = first.Port;
            }
        }
    }

    [RelayCommand]
    private void Start()
    {
        if (string.IsNullOrWhiteSpace(SelectedComPort)) { _store.AppendLog("No COM port selected."); return; }

        var existing = _store.Channels.FirstOrDefault(c => c.PortName == SelectedComPort);
        if (existing?.IsRunning == true) { _store.AppendLog($"{SelectedComPort} is already running."); return; }

        _channelService.OpenChannel(SelectedComPort, BaudRate, [(DefaultUdpAddress, DefaultUdpPort)]);
    }

    [RelayCommand]
    private void Stop()
    {
        if (_store.SelectedChannel is null) { _store.AppendLog("No channel selected. Click a row in Active Channels first."); return; }
        _channelService.StopChannel(_store.SelectedChannel);
    }

    [RelayCommand]
    private void Delete()
    {
        if (_store.SelectedChannel is null) { _store.AppendLog("No channel selected. Click a row in Active Channels first."); return; }
        _channelService.DeleteChannel(_store.SelectedChannel);
    }

    [RelayCommand]
    private void AddUdpDestination()
    {
        if (_store.SelectedChannel is null) { _store.AppendLog("No channel selected. Click a row in Active Channels first."); return; }
        _store.SelectedChannel.UdpDestinations.Add(new UdpDestinationViewModel(DefaultUdpAddress, DefaultUdpPort));
    }

    [RelayCommand]
    private void RefreshComPorts()
    {
        _comPortService.Refresh();
        if (!string.IsNullOrEmpty(SelectedComPort) && _store.AvailableComPorts.Contains(SelectedComPort))
            return;
        SelectedComPort = _store.AvailableComPorts.FirstOrDefault();
    }
}
