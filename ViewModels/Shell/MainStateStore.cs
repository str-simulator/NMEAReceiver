using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows;

namespace NMEAReceiver.ViewModels.Shell;

public sealed partial class MainStateStore : ObservableObject
{
    public ObservableCollection<ChannelRowViewModel> Channels { get; } = new();
    public ObservableCollection<string> AvailableComPorts { get; } = new();

    [ObservableProperty] private ChannelRowViewModel? selectedChannel;
    [ObservableProperty] private bool isRunning;
    [ObservableProperty] private string statusText = "Status: Stopped";
    [ObservableProperty] private string sentenceSnapshot = string.Empty;
    [ObservableProperty] private string logText = string.Empty;

    public void AppendLog(string message)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}";
        if (Application.Current.Dispatcher.CheckAccess())
            LogText += line;
        else
            Application.Current.Dispatcher.Invoke(() => LogText += line);
    }
}
