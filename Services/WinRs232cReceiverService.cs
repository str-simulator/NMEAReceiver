using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
using System.Net;
using System.Net.Sockets;

namespace NMEAReceiver.Services;

public sealed class WinRs232cReceiverService : IWinRs232cReceiverService
{
    private readonly object _sync = new();
    private readonly int _nRcvMaxLen;
    private readonly INmeaSentenceProcessorService _sentenceProcessor;
    private readonly IIosSentenceSocketService _udpSocket;

    private UdpClient? _udpClient;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCancel;
    private NmeaReceiverConfig? _config;

    public event Action<string>? LogMessage;
    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

    public WinRs232cReceiverService(
        INmeaSentenceProcessorService sentenceProcessor,
        IIosSentenceSocketService udpSocket,
        int nRcvMaxLen = 8192)
    {
        _nRcvMaxLen = nRcvMaxLen;
        _sentenceProcessor = sentenceProcessor;
        _udpSocket = udpSocket;

        _sentenceProcessor.SentenceReceived += (ch, s) => SentenceReceived?.Invoke(ch, s);
        _sentenceProcessor.SentenceInfoUpdated += (ch, data) =>
        {
            _udpSocket.SetSentenceInfo(data);
            _udpSocket.SendSentenceInfo(data);
            SentenceInfoUpdated?.Invoke(ch, data);
        };
    }

    public bool Open(NmeaReceiverConfig config)
    {
        lock (_sync)
        {
            if (_udpClient is not null)
                return false;

            _config = config;
            _udpSocket.InitIOSSentenceSocket(config.UdpEndpoints);

            try
            {
                _udpClient = new UdpClient(AddressFamily.InterNetwork);
                _udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, config.UdpBindPort));
            }
            catch (Exception ex)
            {
                _udpClient?.Dispose();
                _udpClient = null;
                OnLog($"{GetPortName()} Bind Fail: {ex.Message}");
                return false;
            }

            _receiveCancel = new CancellationTokenSource();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_receiveCancel.Token));

            var udpDesc = string.Join(", ", config.UdpEndpoints.Select(e => $"{e.Address}:{e.Port}"));
            OnLog($"{GetPortName()} Bind Success -> UDP {udpDesc}");
            return true;
        }
    }

    public void UpdateUdpEndpoints(IEnumerable<(string address, int port)> endpoints)
    {
        lock (_sync)
        {
            _udpSocket.InitIOSSentenceSocket(endpoints);
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            _receiveCancel?.Cancel();
            _udpClient?.Dispose();
            _udpClient = null;

            try
            {
                _receiveTask?.Wait(1500);
            }
            catch
            {
                // Closing the UDP socket is allowed to interrupt ReceiveAsync.
            }

            _receiveTask = null;
            _receiveCancel?.Dispose();
            _receiveCancel = null;

            OnLog($"{GetPortName()} Close");
        }
    }

    public bool IsOpen()
    {
        lock (_sync)
        {
            return _udpClient is not null;
        }
    }

    public int GetPortNo() => _config?.UdpBindPort ?? 0;

    public string GetPortName()
    {
        var port = GetPortNo();
        return port > 0 ? $"UDP:{port}" : "UDP:?";
    }

    private async Task ReceiveLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            UdpClient? udp;
            lock (_sync)
            {
                udp = _udpClient;
            }

            if (udp is null)
                return;

            try
            {
                var result = await udp.ReceiveAsync(token).ConfigureAwait(false);
                var nSize = Math.Min(result.Buffer.Length, _nRcvMaxLen + 1);
                if (nSize <= 0)
                    continue;

                var frame = new byte[nSize];
                Buffer.BlockCopy(result.Buffer, 0, frame, 0, nSize);
                _sentenceProcessor.Receive(GetPortName(), frame, nSize);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (Exception ex)
            {
                OnLog($"{GetPortName()} Receive Error: {ex.Message}");
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
    }

    private void OnLog(string message)
    {
        LogMessage?.Invoke(message);
    }

    public void Dispose()
    {
        Close();
        _sentenceProcessor.Dispose();
        _udpSocket.Dispose();
    }
}
