using CommunityToolkit.Mvvm.ComponentModel;
using NMEAReceiver.Models;
using NMEAReceiver.ViewModels.Shell;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class SnapshotViewModel : ObservableObject
{
    private readonly MainStateStore _store;

    public SnapshotViewModel(MainStateStore store)
    {
        _store = store;
        _store.PropertyChanged += OnStorePropertyChanged;
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.SentenceSnapshot))
            OnPropertyChanged(nameof(SentenceSnapshot));
        else if (e.PropertyName == nameof(MainStateStore.SelectedChannel))
            OnPropertyChanged(nameof(TtmTargets));
    }

    public string SentenceSnapshot => _store.SentenceSnapshot;
    public IEnumerable<TtmTargetData> TtmTargets => _store.SelectedChannel?.TtmTargets is { } targets
        ? targets
        : Array.Empty<TtmTargetData>();
}
