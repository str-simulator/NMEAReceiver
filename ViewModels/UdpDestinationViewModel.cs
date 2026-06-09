using CommunityToolkit.Mvvm.ComponentModel;

namespace NMEAReceiver.ViewModels;

public partial class UdpDestinationViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private string address = "127.0.0.1";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary))]
    private int port = 20011;

    public string Summary => $"{Address}:{Port}";

    public UdpDestinationViewModel() { }

    public UdpDestinationViewModel(string address, int port)
    {
        Address = address;
        Port = port;
    }
}
