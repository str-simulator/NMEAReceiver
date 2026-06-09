using NMEAReceiver.Models;

namespace NMEAReceiver.Services.Interfaces;

public interface INmeaSentenceProcessorService : IDisposable
{
    event Action<string, string>? SentenceReceived;
    event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

    int Receive(string channelName, byte[] lpData, int nSize);
    string GetSentence();
    string GetSentence2();
    void SaveSentenceToLog(string sentence);
    void SetReviceSentence(string channelName, string strRecvSentence);
    void SetSentenceData(int nSentence, string strSentence);
}
