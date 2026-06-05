using NMEAReceiver.Services.Interfaces;
using NMEAReceiver.ViewModels.Shell;
using System.IO.Ports;

namespace NMEAReceiver.Services;

public sealed class ComPortService : IComPortService
{
    private readonly MainStateStore _store;

    public ComPortService(MainStateStore store) => _store = store;

    public void Refresh()
    {
        var ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();
        _store.AvailableComPorts.Clear();
        foreach (var p in ports)
            _store.AvailableComPorts.Add(p);
    }
}
