using NMEAReceiver.Interop;
using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface IIosSentenceSocketService : IDisposable
{
    void InitIOSSentenceSocket(IEnumerable<(string address, int port)> endpoints);
    void SetSentenceInfo(ST_IOSSEND_SENTENCE sentenceInfo);
    ST_IOSSEND_SENTENCE GetSentenceInfo();
    bool SendSentenceInfo(ST_IOSSEND_SENTENCE sentenceInfo);
}
