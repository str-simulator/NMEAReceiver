using CommunityToolkit.Mvvm.ComponentModel;
using NMEAReceiver.ViewModels.Shell;
using System.ComponentModel;

namespace NMEAReceiver.ViewModels.Panels;

public partial class DiagnosticViewModel : ObservableObject
{
    private readonly MainStateStore _store;

    public DiagnosticViewModel(MainStateStore store)
    {
        _store = store;
        _store.PropertyChanged += OnStorePropertyChanged;
    }

    private void OnStorePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainStateStore.RawSentence))
            OnPropertyChanged(nameof(RawSentence));
        else if (e.PropertyName == nameof(MainStateStore.LogText))
            OnPropertyChanged(nameof(LogText));
    }

    public string RawSentence => _store.RawSentence;
    public string LogText => _store.LogText;
}
