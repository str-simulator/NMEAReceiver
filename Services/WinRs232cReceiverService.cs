using NMEAReceiver.Models;
using System.IO.Ports;
using System.Text;

namespace NMEAReceiver.Services;

public sealed class WinRs232cReceiverService : IDisposable
{
    private readonly object _sync = new();
    private readonly byte[] _mData;
    private readonly int _nRcvMaxLen;
    private readonly NmeaSentenceProcessorService _sentenceProcessor = new();
    private readonly IosSentenceSocketService _udpSocket = new();

    private SerialPort? _serialPort;
    private Task? _receiveTask;
    private CancellationTokenSource? _receiveCancel;
    private NmeaReceiverConfig? _config;

    public event Action<string>? LogMessage;
    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

    public WinRs232cReceiverService(int nRcvMaxLen = 8192)
    {
        _nRcvMaxLen = nRcvMaxLen;
        _mData = new byte[_nRcvMaxLen + 1];

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
            if (_serialPort is { IsOpen: true })
                return false;

            _config = config;
            _udpSocket.InitIOSSentenceSocket(config);

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
            _receiveTask = Task.Run(() => ReceiveLoop(_receiveCancel.Token));

            OnLog($"{portName} Open Success → UDP {config.IosSendAddress}:{config.IosSendPortNo}");
            return true;
        }
    }

    public void Close()
    {
        lock (_sync)
        {
            _receiveCancel?.Cancel();

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
            return _serialPort is { IsOpen: true };
        }
    }

    public int GetPortNo()
    {
        return _config?.ComPortNo ?? 0;
    }

    public string GetPortName()
    {
        var portNo = GetPortNo();
        return portNo > 0 ? $"COM{portNo}" : "COM?";
    }

    private void ReceiveLoop(CancellationToken token)
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
