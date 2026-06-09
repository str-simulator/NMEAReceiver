using CommunityToolkit.Mvvm.ComponentModel;
using NMEAReceiver.ViewModels.Shell;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class DiagnosticViewModel : ObservableObject
{
    private readonly MainStateStore _store;
    private ChannelRowViewModel? _trackedChannel;

    public DiagnosticViewModel(MainStateStore store)
    {
        _store = store;
        _store.PropertyChanged += OnStorePropertyChanged;
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.SelectedChannel))
        {
            if (_trackedChannel is not null)
                _trackedChannel.PropertyChanged -= OnTrackedChannelPropertyChanged;

            _trackedChannel = _store.SelectedChannel;

            if (_trackedChannel is not null)
                _trackedChannel.PropertyChanged += OnTrackedChannelPropertyChanged;

            OnPropertyChanged(nameof(RawSentence));
        }
        else if (e.PropertyName == nameof(MainStateStore.LogText))
        {
            OnPropertyChanged(nameof(LogText));
        }
    }

    private void OnTrackedChannelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChannelRowViewModel.RawLog))
            OnPropertyChanged(nameof(RawSentence));
    }

    public string RawSentence => _store.SelectedChannel?.RawLog ?? string.Empty;
    public string LogText => _store.LogText;
}
