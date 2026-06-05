using NMEAReceiver.Interop;
using NMEAReceiver.Models;
using System.Net;
using System.Net.Sockets;

namespace NMEAReceiver.Services;

public sealed class IosSentenceSocketService : IDisposable
{
    private readonly object _sync = new();

    private UdpClient? _udpSocket;
    private IPEndPoint? _sendEndPoint;
    private ST_IOSSEND_SENTENCE _sentenceData;

    public void InitIOSSentenceSocket(NmeaReceiverConfig config)
    {
        lock (_sync)
        {
            CloseSocketInternal();
            _udpSocket = new UdpClient();
            _sendEndPoint = new IPEndPoint(IPAddress.Parse(config.IosSendAddress), config.IosSendPortNo);
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
            if (_udpSocket is null || _sendEndPoint is null)
                return false;

            var payload = StructMarshal.ToBytes(_sentenceData);
            _udpSocket.Send(payload, payload.Length, _sendEndPoint);
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
        _sendEndPoint = null;
    }
}
