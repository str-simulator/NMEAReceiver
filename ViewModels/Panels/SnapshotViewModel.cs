using CommunityToolkit.Mvvm.ComponentModel;
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
    }

    public string SentenceSnapshot => _store.SentenceSnapshot;
}
