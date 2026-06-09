using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface IWinRs232cReceiverService : IDisposable
{
    event Action<string>? LogMessage;
    event Action<string, string>? SentenceReceived;
    event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

    bool Open(NmeaReceiverConfig config);
    void UpdateUdpEndpoints(IEnumerable<(string address, int port)> endpoints);
    void Close();
    bool IsOpen();
    int GetPortNo();
    string GetPortName();
}
