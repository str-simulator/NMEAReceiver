using NMEAReceiver.Interop;
using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
using System.IO;
using System.Net;
using System.Net.Sockets;

namespace NMEAReceiver.Services;

public sealed class IosSentenceSocketService : IIosSentenceSocketService
{
    private readonly object _sync = new();
    private readonly List<IPEndPoint> _sendEndPoints = new();

    private UdpClient? _udpSocket;
    private ST_IOSSEND_SENTENCE _sentenceData;

    public void InitIOSSentenceSocket(IEnumerable<(string address, int port)> endpoints)
    {
        lock (_sync)
        {
            CloseSocketInternal();
            _udpSocket = new UdpClient();
            _sendEndPoints.Clear();
            foreach (var (addr, port) in endpoints)
                _sendEndPoints.Add(new IPEndPoint(IPAddress.Parse(addr), port));
        }
    }

    public void SetSentenceInfo(ST_IOSSEND_SENTENCE sentenceInfo)
    {
        lock (_sync)
        {
            _sentenceData = sentenceInfo;
        }
    }

    public ST_IOSSEND_SENTENCE GetSentenceInfo()
    {
        lock (_sync)
        {
            return _sentenceData;
        }
    }

    public bool SendSentenceInfo(ST_IOSSEND_SENTENCE sentenceInfo, IReadOnlyList<Sentence> updatedSentences)
    {
        lock (_sync)
        {
            if (_udpSocket is null || _sendEndPoints.Count == 0 || updatedSentences.Count == 0)
                return false;

            using var stream = new MemoryStream();
            foreach (var sentence in updatedSentences)
            {
                var body = GetSentenceBodyBytes(sentence, in _sentenceData);
                var header = new SentenceBlockHeader { Type = (int)sentence, Length = body.Length };
                stream.Write(StructMarshal.ToBytes(header));
                stream.Write(body);
            }

            var payload = stream.ToArray();
            foreach (var ep in _sendEndPoints)
                _udpSocket.Send(payload, payload.Length, ep);

            return true;
        }
    }

    private static byte[] GetSentenceBodyBytes(Sentence sentence, in ST_IOSSEND_SENTENCE data) => sentence switch
    {
        Sentence.HTD => StructMarshal.ToBytes(data.m_stSentenceHTD),
        Sentence.RSA => StructMarshal.ToBytes(data.m_stSentenceRSA),
        Sentence.ROR => StructMarshal.ToBytes(data.m_stSentenceROR),
        Sentence.PYDKN => StructMarshal.ToBytes(data.m_stSentencePYDKN),
        Sentence.ALF => StructMarshal.ToBytes(data.m_stSentenceALF),
        Sentence.ALC => StructMarshal.ToBytes(data.m_stSentenceALC),
        Sentence.ARC => StructMarshal.ToBytes(data.m_stSentenceARC),
        Sentence.ACN => StructMarshal.ToBytes(data.m_stSentenceACN),
        Sentence.HBT => StructMarshal.ToBytes(data.m_stSentenceHBT),
        Sentence.RPM => StructMarshal.ToBytes(data.m_stSentenceRPM),
        _ => throw new ArgumentOutOfRangeException(nameof(sentence), sentence, "No IOS block body defined for this sentence type."),
    };

    public void Dispose()
    {
        lock (_sync)
        {
            CloseSocketInternal();
        }
    }

    private void CloseSocketInternal()
    {
        _udpSocket?.Dispose();
        _udpSocket = null;
        _sendEndPoints.Clear();
    }
}
