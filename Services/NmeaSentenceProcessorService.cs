using System.Globalization;
using System.IO;
using System.Text;
using NMEAReceiver.Models;
using NMEAReceiver.Services.Interfaces;

namespace NMEAReceiver.Services;

public sealed class NmeaSentenceProcessorService : INmeaSentenceProcessorService
{
    private readonly object _sync = new();
    private readonly byte[] _pData;
    private readonly StringBuilder _ttmReceiveBuffer = new();

    private ST_IOSSEND_SENTENCE _stIOSSentenceData;
    private string _sentenceHTD = "--HTD,A,,R,R,R,,,,,,,,T,A,A,A,";
    private string _sentenceTest = string.Empty;

    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE, IReadOnlyList<Sentence>>? SentenceInfoUpdated;
    public event Action<string, TtmTargetData>? TtmTargetUpdated;

    public NmeaSentenceProcessorService(int nRcvMaxLen = 8192)
    {
        _pData = new byte[nRcvMaxLen + 1];
    }

    public int Receive(string channelName, byte[] lpData, int nSize)
    {
        lock (_sync)
        {
            Array.Clear(_pData, 0, _pData.Length);
            Buffer.BlockCopy(lpData, 0, _pData, 0, Math.Min(lpData.Length, _pData.Length));

            var strSentence = Encoding.ASCII.GetString(_pData, 0, nSize);
            SaveSentenceToLog(strSentence);

            SentenceReceived?.Invoke(channelName, strSentence);
            ProcessTtmReceiveBuffer(channelName, strSentence);
            SetReviceSentence(channelName, strSentence);

            return nSize;
        }
    }

    public string GetSentence()
    {
        lock (_sync)
        {
            return _sentenceHTD;
        }
    }

    public string GetSentence2()
    {
        lock (_sync)
        {
            return _sentenceTest;
        }
    }

    public void SaveSentenceToLog(string sentence)
    {
        var logPath = Path.Combine(Environment.CurrentDirectory, "nmea_log.txt");
        File.AppendAllText(logPath, sentence + Environment.NewLine, Encoding.ASCII);
    }

    public void SetReviceSentence(string channelName, string strRecvSentence)
    {
        _sentenceTest = strRecvSentence.Length > 255
            ? strRecvSentence[..255]
            : strRecvSentence;

        var updatedSentences = new List<Sentence>();
        var ncount = strRecvSentence.Count(c => c == '$');

        for (var i = 0; i <= ncount; i++)
        {
            var strSentence = ExtractSubString(strRecvSentence, i, '$');
            var formatter = GetSentenceFormatter(strSentence);

            switch (formatter)
            {
                case nameof(Sentence.HTD):
                    strSentence = "$" + strSentence;
                    SetSentenceData((int)Sentence.HTD, strSentence);
                    updatedSentences.Add(Sentence.HTD);
                    break;
                case nameof(Sentence.RSA):
                    SetSentenceData((int)Sentence.RSA, strSentence);
                    updatedSentences.Add(Sentence.RSA);
                    break;
                case nameof(Sentence.ROR):
                    SetSentenceData((int)Sentence.ROR, strSentence);
                    updatedSentences.Add(Sentence.ROR);
                    break;
                case nameof(Sentence.PYDKN):
                    SetSentenceData((int)Sentence.PYDKN, strSentence);
                    updatedSentences.Add(Sentence.PYDKN);
                    break;
                case nameof(Sentence.ALF):
                    SetSentenceData((int)Sentence.ALF, strSentence);
                    updatedSentences.Add(Sentence.ALF);
                    break;
                case nameof(Sentence.ALC):
                    SetSentenceData((int)Sentence.ALC, strSentence);
                    updatedSentences.Add(Sentence.ALC);
                    break;
                case nameof(Sentence.ARC):
                    SetSentenceData((int)Sentence.ARC, strSentence);
                    updatedSentences.Add(Sentence.ARC);
                    break;
                case nameof(Sentence.ACN):
                    SetSentenceData((int)Sentence.ACN, strSentence);
                    updatedSentences.Add(Sentence.ACN);
                    break;
                case nameof(Sentence.HBT):
                    SetSentenceData((int)Sentence.HBT, strSentence);
                    updatedSentences.Add(Sentence.HBT);
                    break;
                case nameof(Sentence.RPM):
                    SetSentenceData((int)Sentence.RPM, strSentence);
                    updatedSentences.Add(Sentence.RPM);
                    break;
            }
        }

        if (updatedSentences.Count > 0)
            SentenceInfoUpdated?.Invoke(channelName, _stIOSSentenceData, updatedSentences);
    }

    private static string GetSentenceFormatter(string sentence)
    {
        var header = sentence.TrimStart('$', '!');
        var delimiterIndex = header.IndexOfAny(new[] { ',', '*', '\r', '\n' });
        if (delimiterIndex >= 0)
            header = header[..delimiterIndex];

        if (header.StartsWith('P'))
            return header.ToUpperInvariant();

        return header.Length >= 3 ? header[^3..].ToUpperInvariant() : string.Empty;
    }

    public void SetSentenceData(int nSentence, string strSentence)
    {
        switch ((Sentence)nSentence)
        {
            case Sentence.HTD:
                _sentenceHTD = strSentence;
                _stIOSSentenceData.m_stSentenceHTD.szOverride = GetCharField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceHTD.dRudderAngle = GetDoubleField(strSentence, 2, ',');

                var rudderDirection = ExtractSubString(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceHTD.nRudderDirenction =
                    string.Equals(rudderDirection, "L", StringComparison.OrdinalIgnoreCase) ? -1 : 1;

                var steeringMode = ExtractSubString(strSentence, 4, ',');
                if (string.Equals(steeringMode, "M", StringComparison.OrdinalIgnoreCase))
                {
                    _stIOSSentenceData.m_stSentenceHTD.nSteeringMode = 0;
                }
                else if (string.Equals(steeringMode, "S", StringComparison.OrdinalIgnoreCase))
                {
                    _stIOSSentenceData.m_stSentenceHTD.nSteeringMode = 1;
                }
                else
                {
                    _stIOSSentenceData.m_stSentenceHTD.nSteeringMode = 2;
                }

                _stIOSSentenceData.m_stSentenceHTD.szTurnMode = GetCharField(strSentence, 5, ',');
                _stIOSSentenceData.m_stSentenceHTD.dCommandedRudderLimit = GetDoubleField(strSentence, 6, ',');
                _stIOSSentenceData.m_stSentenceHTD.dCommandedOffHeadingLimit = GetDoubleField(strSentence, 7, ',');
                _stIOSSentenceData.m_stSentenceHTD.dRadiusofTurnHeadingChanges = GetDoubleField(strSentence, 8, ',');
                _stIOSSentenceData.m_stSentenceHTD.dRateofTurnHeadingChanges = GetDoubleField(strSentence, 9, ',');
                _stIOSSentenceData.m_stSentenceHTD.dCommandedHeadingSteer = GetDoubleField(strSentence, 10, ',');
                _stIOSSentenceData.m_stSentenceHTD.dCommandedOffTrackLimit = GetDoubleField(strSentence, 11, ',');
                _stIOSSentenceData.m_stSentenceHTD.dCommandedTrack = GetDoubleField(strSentence, 12, ',');
                _stIOSSentenceData.m_stSentenceHTD.szHeadingReferenceUse = GetCharField(strSentence, 13, ',');
                _stIOSSentenceData.m_stSentenceHTD.szRudderStatus = GetCharField(strSentence, 14, ',');
                _stIOSSentenceData.m_stSentenceHTD.szOffHeadingStatus = GetCharField(strSentence, 15, ',');
                _stIOSSentenceData.m_stSentenceHTD.szOffTrackStatus = GetCharField(strSentence, 16, ',');
                _stIOSSentenceData.m_stSentenceHTD.dVesselHeading = GetDoubleField(strSentence, 17, ',');
                break;

            case Sentence.RSA:
                _stIOSSentenceData.m_stSentenceRSA.dStarboardRudderSensor = GetDoubleField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceRSA.dPortRudderSensor = GetDoubleField(strSentence, 3, ',');
                break;

            case Sentence.ROR:
                _stIOSSentenceData.m_stSentenceROR.dStarboardRudderOrder = GetDoubleField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceROR.dPortRudderOrder = GetDoubleField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceROR.szCommandedSourceLocation = GetCharField(strSentence, 5, ',');
                break;

            case Sentence.PYDKN:
                _stIOSSentenceData.m_stSentencePYDKN.szNFCommand = GetCharField(strSentence, 1, ',');

                var pydknMode = ExtractSubString(strSentence, 2, ',');
                if (string.Equals(pydknMode, "A", StringComparison.OrdinalIgnoreCase))
                {
                    _stIOSSentenceData.m_stSentencePYDKN.nSteeringMode = 1;
                }
                else if (string.Equals(pydknMode, "V", StringComparison.OrdinalIgnoreCase))
                {
                    _stIOSSentenceData.m_stSentencePYDKN.nSteeringMode = 0;
                }

                _stIOSSentenceData.m_stSentencePYDKN.nSTBDRudderCommnad = RudderCommandToInt(ExtractSubString(strSentence, 3, ','));
                _stIOSSentenceData.m_stSentencePYDKN.nPORTRudderCommand = RudderCommandToInt(ExtractSubString(strSentence, 4, ','));
                break;

            case Sentence.ALF:
                _stIOSSentenceData.m_stSentenceALF.nTotalALFSentence = GetIntField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceALF.nSentenceNumber = GetIntField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceALF.nSequentialMessage = GetIntField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceALF.szTimeLastChange = GetCharField(strSentence, 4, ',');
                _stIOSSentenceData.m_stSentenceALF.szAlertCategory = GetCharField(strSentence, 5, ',');
                _stIOSSentenceData.m_stSentenceALF.szAlertPriority = GetCharField(strSentence, 6, ',');
                _stIOSSentenceData.m_stSentenceALF.szAlertState = GetCharField(strSentence, 7, ',');
                _stIOSSentenceData.m_stSentenceALF.szManufacturerMnemonicCode = GetCharField(strSentence, 8, ',');
                _stIOSSentenceData.m_stSentenceALF.dAlertIdentifier = GetDoubleField(strSentence, 9, ',');
                _stIOSSentenceData.m_stSentenceALF.dAlertInstance = GetDoubleField(strSentence, 10, ',');
                _stIOSSentenceData.m_stSentenceALF.dRevisionCounter = GetDoubleField(strSentence, 11, ',');
                _stIOSSentenceData.m_stSentenceALF.nEscalationcounter = GetIntField(strSentence, 12, ',');
                _stIOSSentenceData.m_stSentenceALF.szAlertText = GetCharField(strSentence, 13, ',');
                break;

            case Sentence.ALC:
                _stIOSSentenceData.m_stSentenceALC.nTotalALFSentence = GetIntField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceALC.nSentenceNumber = GetIntField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceALC.nSequentialMessage = GetIntField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceALC.dNumberAlertEntries = GetDoubleField(strSentence, 4, ',');
                _stIOSSentenceData.m_stSentenceALC.szManufacturerMnemonicCode = GetCharField(strSentence, 5, ',');
                _stIOSSentenceData.m_stSentenceALC.dAlertIdentifier = GetDoubleField(strSentence, 6, ',');
                _stIOSSentenceData.m_stSentenceALC.dAlertInstance = GetDoubleField(strSentence, 7, ',');
                _stIOSSentenceData.m_stSentenceALC.dRevisionCounter = GetDoubleField(strSentence, 8, ',');
                _stIOSSentenceData.m_stSentenceALC.szAdditionalA0lertEntries = GetCharField(strSentence, 9, ',');
                _stIOSSentenceData.m_stSentenceALC.szAlertEntry = GetCharField(strSentence, 10, ',');
                break;

            case Sentence.ARC:
                _stIOSSentenceData.m_stSentenceARC.szTime = GetCharField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceARC.szManufacturerMnemonicCode = GetCharField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceARC.szAlertIdentifier = GetCharField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceARC.dAlertInstance = GetDoubleField(strSentence, 4, ',');
                _stIOSSentenceData.m_stSentenceARC.szAlertCommand = GetCharField(strSentence, 5, ',');
                break;

            case Sentence.ACN:
                _stIOSSentenceData.m_stSentenceACN.szTime = GetCharField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceACN.szManufacturerMnemonicCode = GetCharField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceACN.dAlertIdentifier = GetDoubleField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceACN.dAlertInstance = GetDoubleField(strSentence, 4, ',');
                _stIOSSentenceData.m_stSentenceACN.szAlertCommand = GetCharField(strSentence, 5, ',');
                _stIOSSentenceData.m_stSentenceACN.szSentenceStatusFlag = GetCharField(strSentence, 6, ',');
                break;

            case Sentence.HBT:
                _stIOSSentenceData.m_stSentenceHBT.nConfiguredRepeatInterval = GetIntField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceHBT.szEquipmentStatus = GetCharField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceHBT.nSequentialSentenceIdentifier = GetIntField(strSentence, 3, ',');
                break;

            case Sentence.RPM:
                _stIOSSentenceData.m_stSentenceRPM.szSource = GetCharField(strSentence, 1, ',');
                _stIOSSentenceData.m_stSentenceRPM.nEngineOrShaftNumber = (int)GetDoubleField(strSentence, 2, ',');
                _stIOSSentenceData.m_stSentenceRPM.dSpeed = GetDoubleField(strSentence, 3, ',');
                _stIOSSentenceData.m_stSentenceRPM.dPropellerPitch = GetDoubleField(strSentence, 4, ',');
                _stIOSSentenceData.m_stSentenceRPM.szStatus = GetCharField(strSentence, 5, ',');
                break;

            default:
                break;
        }
    }

    public void Dispose()
    {
    }

    private static int RudderCommandToInt(string command)
    {
        if (string.IsNullOrEmpty(command))
        {
            return 0;
        }

        return string.Equals(command, "S", StringComparison.OrdinalIgnoreCase) ? 1 : -1;
    }

    private void ProcessTtmReceiveBuffer(string channelName, string receivedText)
    {
        _ttmReceiveBuffer.Append(receivedText);

        while (_ttmReceiveBuffer.Length > 0)
        {
            var sentenceStart = IndexOf(_ttmReceiveBuffer, '$', 0);
            if (sentenceStart < 0)
            {
                _ttmReceiveBuffer.Clear();
                return;
            }

            if (sentenceStart > 0)
                _ttmReceiveBuffer.Remove(0, sentenceStart);

            var lineEnd = IndexOf(_ttmReceiveBuffer, '\n', 0);
            var nextSentenceStart = IndexOf(_ttmReceiveBuffer, '$', 1);
            var checksumEnd = FindChecksumEnd(_ttmReceiveBuffer);

            var sentenceLength = 0;
            if (lineEnd >= 0 && (nextSentenceStart < 0 || lineEnd < nextSentenceStart))
                sentenceLength = lineEnd + 1;
            else if (checksumEnd > 0 &&
                     (nextSentenceStart < 0 || checksumEnd <= nextSentenceStart))
                sentenceLength = checksumEnd;
            else if (nextSentenceStart > 0)
                sentenceLength = nextSentenceStart;

            if (sentenceLength == 0)
            {
                if (_ttmReceiveBuffer.Length > _pData.Length)
                    _ttmReceiveBuffer.Clear();
                return;
            }

            var sentence = _ttmReceiveBuffer.ToString(0, sentenceLength)
                .Trim('\r', '\n', '\0', ' ');
            _ttmReceiveBuffer.Remove(0, sentenceLength);

            if (TryParseTtm(sentence, out var target))
                TtmTargetUpdated?.Invoke(channelName, target);
        }
    }

    private static bool TryParseTtm(string sentence, out TtmTargetData target)
    {
        target = null!;
        if (string.IsNullOrWhiteSpace(sentence) || sentence[0] != '$')
            return false;

        var checksumIndex = sentence.IndexOf('*');
        var body = sentence;
        if (checksumIndex >= 0)
        {
            if (checksumIndex + 2 >= sentence.Length ||
                !byte.TryParse(
                    sentence.AsSpan(checksumIndex + 1, 2),
                    NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture,
                    out var expectedChecksum))
            {
                return false;
            }

            byte calculatedChecksum = 0;
            for (var i = 1; i < checksumIndex; i++)
                calculatedChecksum ^= (byte)sentence[i];

            if (calculatedChecksum != expectedChecksum)
                return false;

            body = sentence[..checksumIndex];
        }

        var fields = body.Split(',');
        if (fields.Length < 15 ||
            !string.Equals(GetSentenceFormatter(fields[0]), nameof(Sentence.TTM), StringComparison.Ordinal) ||
            !int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var targetNumber) ||
            targetNumber is < 0 or > 999)
        {
            return false;
        }

        target = new TtmTargetData
        {
            TargetNumber = targetNumber,
            Distance = ParseNullableDouble(fields[2]),
            Bearing = ParseNullableDouble(fields[3]),
            BearingReference = ParseNullableChar(fields[4]),
            Speed = ParseNullableDouble(fields[5]),
            Course = ParseNullableDouble(fields[6]),
            CourseReference = ParseNullableChar(fields[7]),
            CpaDistance = ParseNullableDouble(fields[8]),
            TimeToCpaMinutes = ParseNullableDouble(fields[9]),
            Unit = ParseNullableChar(fields[10]),
            TargetName = fields[11],
            Status = ParseNullableChar(fields[12]),
            IsReferenceTarget = string.Equals(fields[13], "R", StringComparison.OrdinalIgnoreCase),
            DataTimeUtc = ParseUtcTime(fields[14]),
            AcquisitionType = fields.Length > 15 ? ParseNullableChar(fields[15]) : null,
            ReceivedAtUtc = DateTime.UtcNow,
        };
        return true;
    }

    private static double? ParseNullableDouble(string value)
        => double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static char? ParseNullableChar(string value)
        => string.IsNullOrWhiteSpace(value) ? null : char.ToUpperInvariant(value[0]);

    private static TimeSpan? ParseUtcTime(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numericTime))
            return null;

        var hours = (int)(numericTime / 10000);
        var minutes = (int)(numericTime / 100) % 100;
        var seconds = numericTime % 100;
        if (hours is < 0 or > 23 || minutes is < 0 or > 59 || seconds is < 0 or >= 60)
            return null;

        return TimeSpan.FromHours(hours) +
               TimeSpan.FromMinutes(minutes) +
               TimeSpan.FromSeconds(seconds);
    }

    private static int FindChecksumEnd(StringBuilder source)
    {
        var checksumStart = IndexOf(source, '*', 0);
        if (checksumStart < 0 || checksumStart + 2 >= source.Length)
            return -1;

        return IsHexDigit(source[checksumStart + 1]) && IsHexDigit(source[checksumStart + 2])
            ? checksumStart + 3
            : -1;
    }

    private static bool IsHexDigit(char value)
        => value is >= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f';

    private static int IndexOf(StringBuilder source, char value, int startIndex)
    {
        for (var i = startIndex; i < source.Length; i++)
        {
            if (source[i] == value)
                return i;
        }

        return -1;
    }

    private static string ExtractSubString(string source, int index, char delimiter)
    {
        if (string.IsNullOrEmpty(source) || index < 0)
        {
            return string.Empty;
        }

        var parts = source.Split(delimiter);
        return index < parts.Length ? parts[index] : string.Empty;
    }

    private static double GetDoubleField(string source, int index, char delimiter)
    {
        var token = ExtractSubString(source, index, delimiter);
        return double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0.0;
    }

    private static int GetIntField(string source, int index, char delimiter)
    {
        var token = ExtractSubString(source, index, delimiter);
        return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private static char GetCharField(string source, int index, char delimiter)
    {
        var token = ExtractSubString(source, index, delimiter);
        return token.Length > 0 ? token[0] : '\0';
    }
}
