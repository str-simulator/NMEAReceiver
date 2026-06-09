using NMEAReceiver.Interop;
using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
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

    public bool SendSentenceInfo(ST_IOSSEND_SENTENCE sentenceInfo)
    {
        lock (_sync)
        {
            if (_udpSocket is null || _sendEndPoints.Count == 0)
                return false;

            var payload = StructMarshal.ToBytes(_sentenceData);
            foreach (var ep in _sendEndPoints)
                _udpSocket.Send(payload, payload.Length, ep);

            return true;
        }
    }

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
