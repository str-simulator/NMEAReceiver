using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NMEAReceiver.Services;
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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSerialMode))]
    [NotifyPropertyChangedFor(nameof(IsUdpMode))]
    [NotifyPropertyChangedFor(nameof(IsSerialInputEnabled))]
    [NotifyPropertyChangedFor(nameof(IsUdpInputEnabled))]
    private ReceiverInputMode selectedInputMode = ReceiverInputMode.Serial;

    [ObservableProperty] private string? selectedComPort;
    [ObservableProperty] private int baudRate = 38400;
    [ObservableProperty] private int udpBindPort = 40014;
    [ObservableProperty] private string defaultUdpAddress = "127.0.0.1";
    [ObservableProperty] private int defaultUdpPort = 20011;

    public ObservableCollection<string> AvailableComPorts => _store.AvailableComPorts;
    public string StatusText => _store.StatusText;

    public bool IsSerialMode
    {
        get => SelectedInputMode == ReceiverInputMode.Serial;
        set
        {
            if (value)
                SelectedInputMode = ReceiverInputMode.Serial;
        }
    }

    public bool IsUdpMode
    {
        get => SelectedInputMode == ReceiverInputMode.Udp;
        set
        {
            if (value)
                SelectedInputMode = ReceiverInputMode.Udp;
        }
    }

    public bool IsSerialInputEnabled => SelectedInputMode == ReceiverInputMode.Serial;
    public bool IsUdpInputEnabled => SelectedInputMode == ReceiverInputMode.Udp;

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
            SelectedInputMode = ch.InputMode;
            if (ch.InputMode == ReceiverInputMode.Udp)
            {
                UdpBindPort = ch.PortNo;
            }
            else
            {
                SelectedComPort = _store.AvailableComPorts.Contains(ch.PortName) ? ch.PortName : SelectedComPort;
                BaudRate = ch.BaudRate;
            }

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
        if (!TryCreateConfig(out var config))
            return;

        var channelName = GetChannelName(config);
        var existing = _store.Channels.FirstOrDefault(c => c.PortName == channelName);
        if (existing?.IsRunning == true) { _store.AppendLog($"{channelName} is already running."); return; }

        if (existing is not null)
            config.UdpEndpoints = existing.UdpDestinations.Select(d => (d.Address, d.Port)).ToList();

        _channelService.OpenChannel(config);
    }

    [RelayCommand]
    private void Stop()
    {
        if (_store.SelectedChannel is null) { _store.AppendLog("No channel selected. Click a row in Active Channels first."); return; }
        _channelService.StopChannel(_store.SelectedChannel.PortName);
    }

    [RelayCommand]
    private void Delete()
    {
        if (_store.SelectedChannel is null) { _store.AppendLog("No channel selected. Click a row in Active Channels first."); return; }
        _channelService.DeleteChannel(_store.SelectedChannel.PortName);
    }

    [RelayCommand]
    private void AddUdpDestination()
    {
        if (!TryCreateConfig(out var config))
            return;

        var channelName = GetChannelName(config);
        var channel = _store.Channels.FirstOrDefault(c => c.PortName == channelName);
        if (channel is null)
        {
            _channelService.ConfigureChannel(config);
            return;
        }

        var exists = channel.UdpDestinations.Any(d =>
            string.Equals(d.Address, DefaultUdpAddress, StringComparison.OrdinalIgnoreCase)
            && d.Port == DefaultUdpPort);

        if (exists)
        {
            _store.SelectedChannel = channel;
            _store.AppendLog($"{channelName} already has UDP destination {DefaultUdpAddress}:{DefaultUdpPort}.");
            return;
        }

        _store.SelectedChannel = channel;
        channel.UdpDestinations.Add(new UdpDestinationViewModel(DefaultUdpAddress, DefaultUdpPort));
    }

    [RelayCommand]
    private void RefreshComPorts()
    {
        _comPortService.Refresh();
        if (!string.IsNullOrEmpty(SelectedComPort) && _store.AvailableComPorts.Contains(SelectedComPort))
            return;
        SelectedComPort = _store.AvailableComPorts.FirstOrDefault();
    }

    private bool TryCreateConfig(out NmeaReceiverConfig config)
    {
        config = new NmeaReceiverConfig
        {
            InputMode = SelectedInputMode,
            BaudRate = BaudRate,
            UdpBindPort = UdpBindPort,
            UdpEndpoints = [(DefaultUdpAddress, DefaultUdpPort)],
        };

        if (string.IsNullOrWhiteSpace(DefaultUdpAddress))
        {
            _store.AppendLog("UDP destination address is empty.");
            return false;
        }

        if (DefaultUdpPort is <= 0 or > 65535)
        {
            _store.AppendLog("Invalid UDP destination port.");
            return false;
        }

        if (SelectedInputMode == ReceiverInputMode.Udp)
        {
            if (UdpBindPort is <= 0 or > 65535)
            {
                _store.AppendLog("Invalid UDP bind port.");
                return false;
            }

            return true;
        }

        if (string.IsNullOrWhiteSpace(SelectedComPort))
        {
            _store.AppendLog("No COM port selected.");
            return false;
        }

        if (!int.TryParse(SelectedComPort.Replace("COM", string.Empty, StringComparison.OrdinalIgnoreCase), out var portNo))
        {
            _store.AppendLog($"Invalid COM port: {SelectedComPort}");
            return false;
        }

        config.ComPortNo = portNo;
        return true;
    }

    private static string GetChannelName(NmeaReceiverConfig config)
        => config.InputMode == ReceiverInputMode.Udp ? $"UDP:{config.UdpBindPort}" : $"COM{config.ComPortNo}";
}
