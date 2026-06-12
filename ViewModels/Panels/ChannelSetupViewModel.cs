using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels;
using NMEAReceiver.ViewModels.Shell;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class ChannelSetupViewModel : ObservableObject
{
    private readonly MainStateStore _store;
    private readonly IReceiverChannelService _channelService;

    [ObservableProperty] private int udpBindPort = 40014;
    [ObservableProperty] private string defaultUdpAddress = "127.0.0.1";
    [ObservableProperty] private int defaultUdpPort = 20011;

    public string StatusText => _store.StatusText;

    public ChannelSetupViewModel(
        MainStateStore store,
        IReceiverChannelService channelService)
    {
        _store = store;
        _channelService = channelService;

        _store.PropertyChanged += OnStorePropertyChanged;
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.StatusText))
            OnPropertyChanged(nameof(StatusText));

        if (e.PropertyName == nameof(MainStateStore.SelectedChannel) && _store.SelectedChannel is { } ch)
        {
            UdpBindPort = ch.PortNo;

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
        if (!ValidateUdpInput())
            return;

        var channelName = $"UDP:{UdpBindPort}";
        var existing = _store.Channels.FirstOrDefault(c => c.PortName == channelName);
        if (existing?.IsRunning == true) { _store.AppendLog($"{channelName} is already running."); return; }

        var destinations = existing?.UdpDestinations
            .Select(d => (d.Address, d.Port))
            .ToList();

        if (destinations is null || destinations.Count == 0)
            destinations = [(DefaultUdpAddress, DefaultUdpPort)];

        _channelService.OpenChannel(UdpBindPort.ToString(), destinations);
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
        if (!ValidateUdpInput())
            return;

        var channelName = $"UDP:{UdpBindPort}";
        var channel = _store.Channels.FirstOrDefault(c => c.PortName == channelName);

        if (channel is null)
        {
            _channelService.ConfigureChannel(UdpBindPort.ToString(), [(DefaultUdpAddress, DefaultUdpPort)]);
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

    private bool ValidateUdpInput()
    {
        if (UdpBindPort is <= 0 or > 65535)
        {
            _store.AppendLog("Invalid UDP bind port.");
            return false;
        }

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

        return true;
    }
}
