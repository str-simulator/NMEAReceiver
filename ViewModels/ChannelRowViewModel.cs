using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;

namespace NMEAReceiver.ViewModels;

public partial class ChannelRowViewModel : ObservableObject
{
    [ObservableProperty] private int portNo;
    [ObservableProperty] private string portName = string.Empty;
    [ObservableProperty] private string status = "Stopped";
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string lastUpdated = string.Empty;
    [ObservableProperty] private string rawLog = string.Empty;

    public ObservableCollection<UdpDestinationViewModel> UdpDestinations { get; } = new();

    public string UdpDestinationsSummary =>
        UdpDestinations.Count == 0
            ? "(none)"
            : string.Join(", ", UdpDestinations.Select(d => d.Summary));

    public ChannelRowViewModel(int portNo, IEnumerable<(string address, int port)>? udpDestinations = null)
    {
        PortNo = portNo;
        PortName = $"UDP:{portNo}";

        foreach (var (addr, p) in udpDestinations ?? new[] { ("127.0.0.1", 20011) })
        {
            var dest = new UdpDestinationViewModel(addr, p);
            dest.PropertyChanged += OnDestinationPropertyChanged;
            UdpDestinations.Add(dest);
        }

        UdpDestinations.CollectionChanged += OnUdpDestinationsChanged;
    }

    public void AppendRawLog(string sentence)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {sentence}{Environment.NewLine}";
        if (Application.Current.Dispatcher.CheckAccess())
        {
            RawLog += line;
            LastUpdated = DateTime.Now.ToString("HH:mm:ss.fff");
        }
        else
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                RawLog += line;
                LastUpdated = DateTime.Now.ToString("HH:mm:ss.fff");
            });
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveUdpDestination))]
    private void RemoveUdpDestination(UdpDestinationViewModel dest)
        => UdpDestinations.Remove(dest);

    private bool CanRemoveUdpDestination(UdpDestinationViewModel? dest)
        => UdpDestinations.Count > 1;

    private void OnUdpDestinationsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
            foreach (UdpDestinationViewModel item in e.NewItems)
                item.PropertyChanged += OnDestinationPropertyChanged;

        if (e.OldItems is not null)
            foreach (UdpDestinationViewModel item in e.OldItems)
                item.PropertyChanged -= OnDestinationPropertyChanged;

        OnPropertyChanged(nameof(UdpDestinationsSummary));
        OnPropertyChanged(nameof(UdpDestinations));
        RemoveUdpDestinationCommand.NotifyCanExecuteChanged();
    }

    private void OnDestinationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        OnPropertyChanged(nameof(UdpDestinationsSummary));
        OnPropertyChanged(nameof(UdpDestinations));
    }
}
