using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace NMEAReceiver.Services;

public sealed class WinRs232cReceiverService : IWinRs232cReceiverService
{
    private readonly object _sync = new();
    private readonly byte[] _mData;
    private readonly int _nRcvMaxLen;
    private readonly INmeaSentenceProcessorService _sentenceProcessor;
    private readonly IIosSentenceSocketService _udpSocket;

    private SerialPort? _serialPort;
    private UdpClient? _udpClient;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCancel;
    private NmeaReceiverConfig? _config;

    public event Action<string>? LogMessage;
    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

    public WinRs232cReceiverService(INmeaSentenceProcessorService sentenceProcessor, IIosSentenceSocketService udpSocket, int nRcvMaxLen = 8192)
    {
        _nRcvMaxLen = nRcvMaxLen;
        _mData = new byte[_nRcvMaxLen + 1];
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
            if (IsOpenCore())
                return false;

            _config = config;
            _udpSocket.InitIOSSentenceSocket(config.UdpEndpoints);

            return config.InputMode == ReceiverInputMode.Udp
                ? OpenUdp(config)
                : OpenSerial(config);
        }
    }

    private bool OpenSerial(NmeaReceiverConfig config)
    {
        var portName = GetPortName();
        _serialPort = new SerialPort(portName)
        {
            BaudRate = config.BaudRate,
            DataBits = 8,
            Parity = Parity.None,
            StopBits = StopBits.One,
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = 1000,
            WriteTimeout = 1000,
            Encoding = Encoding.ASCII
        };

        try
        {
            _serialPort.Open();
        }
        catch (Exception ex)
        {
            _serialPort.Dispose();
            _serialPort = null;
            OnLog($"{portName} Open Fail: {ex.Message}");
            return false;
        }

        _receiveCancel = new CancellationTokenSource();
        _receiveTask = Task.Run(() => ReceiveSerialLoop(_receiveCancel.Token));

        var udpDesc = string.Join(", ", config.UdpEndpoints.Select(e => $"{e.Address}:{e.Port}"));
        OnLog($"{portName} Open Success -> UDP {udpDesc}");
        return true;
    }

    private bool OpenUdp(NmeaReceiverConfig config)
    {
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
        _receiveTask = Task.Run(() => ReceiveUdpLoopAsync(_receiveCancel.Token));

        var udpDesc = string.Join(", ", config.UdpEndpoints.Select(e => $"{e.Address}:{e.Port}"));
        OnLog($"{GetPortName()} Bind Success -> UDP {udpDesc}");
        return true;
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
                // Keep close behavior tolerant.
            }

            _receiveTask = null;
            _receiveCancel?.Dispose();
            _receiveCancel = null;

            if (_serialPort is not null)
            {
                if (_serialPort.IsOpen)
                    _serialPort.Close();

                _serialPort.Dispose();
                _serialPort = null;
            }

            OnLog($"{GetPortName()} Close");
        }
    }

    public bool IsOpen()
    {
        lock (_sync)
        {
            return IsOpenCore();
        }
    }

    private bool IsOpenCore() => _serialPort is { IsOpen: true } || _udpClient is not null;

    public int GetPortNo()
    {
        if (_config?.InputMode == ReceiverInputMode.Udp)
            return _config.UdpBindPort;

        return _config?.ComPortNo ?? 0;
    }

    public string GetPortName()
    {
        var portNo = GetPortNo();
        if (_config?.InputMode == ReceiverInputMode.Udp)
            return portNo > 0 ? $"UDP:{portNo}" : "UDP:?";

        return portNo > 0 ? $"COM{portNo}" : "COM?";
    }

    private void ReceiveSerialLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            SerialPort? serial;
            lock (_sync)
            {
                serial = _serialPort;
            }

            if (serial is null || !serial.IsOpen)
                return;

            try
            {
                if (serial.BytesToRead <= 0)
                {
                    Thread.Sleep(5);
                    continue;
                }

                Thread.Sleep(250);
                Array.Clear(_mData, 0, _mData.Length);

                var readLength = Math.Min(_nRcvMaxLen + 1, Math.Max(1, serial.BytesToRead));
                var nSize = serial.Read(_mData, 0, readLength);

                if (nSize > 0)
                {
                    var frame = new byte[nSize];
                    Buffer.BlockCopy(_mData, 0, frame, 0, nSize);
                    _sentenceProcessor.Receive(GetPortName(), frame, nSize);
                }
            }
            catch (TimeoutException)
            {
                // Keep loop alive.
            }
            catch (Exception ex)
            {
                OnLog($"{GetPortName()} Receive Error: {ex.Message}");
                Thread.Sleep(50);
            }
        }
    }

    private async Task ReceiveUdpLoopAsync(CancellationToken token)
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
                try
                {
                    await Task.Delay(50, token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
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
