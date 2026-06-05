using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class ChannelsTableViewModel : ObservableObject
{
    private readonly MainStateStore _store;
    private readonly IReceiverChannelService _channelService;

    public ChannelsTableViewModel(MainStateStore store, IReceiverChannelService channelService)
    {
        _store = store;
        _channelService = channelService;
        _store.PropertyChanged += OnStorePropertyChanged;
    }

    [RelayCommand]
    private void DeleteChannel(ChannelRowViewModel channel) => _channelService.DeleteChannel(channel);

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.SelectedChannel))
            OnPropertyChanged(nameof(SelectedChannel));
    }

    public ObservableCollection<ChannelRowViewModel> Channels => _store.Channels;

    public ChannelRowViewModel? SelectedChannel
    {
        get => _store.SelectedChannel;
        set => _store.SelectedChannel = value;
    }
}
