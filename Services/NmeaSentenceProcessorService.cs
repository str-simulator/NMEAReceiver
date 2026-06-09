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

    private ST_IOSSEND_SENTENCE _stIOSSentenceData;
    private string _sentenceHTD = "--HTD,A,,R,R,R,,,,,,,,T,A,A,A,";
    private string _sentenceTest = string.Empty;

    public event Action<string, string>? SentenceReceived;
    public event Action<string, ST_IOSSEND_SENTENCE>? SentenceInfoUpdated;

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

        var ncount = strRecvSentence.Count(c => c == '$');

        for (var i = 0; i <= ncount; i++)
        {
            var strSentence = ExtractSubString(strRecvSentence, i, '$');

            if (strSentence.Contains("HTD", StringComparison.Ordinal))
            {
                strSentence = "$" + strSentence;
                SetSentenceData((int)Sentence.HTD, strSentence);
            }
            else if (strSentence.Contains("RSA", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.RSA, strSentence);
            }
            else if (strSentence.Contains("ROR", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.ROR, strSentence);
            }
            else if (strSentence.Contains("PYDKN", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.PYDKN, strSentence);
            }
            else if (strSentence.Contains("ALF", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.ALF, strSentence);
            }
            else if (strSentence.Contains("ALC", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.ALC, strSentence);
            }
            else if (strSentence.Contains("ARC", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.ARC, strSentence);
            }
            else if (strSentence.Contains("ACN", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.ACN, strSentence);
            }
            else if (strSentence.Contains("HBT", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.HBT, strSentence);
            }
            else if (strSentence.Contains("GGA", StringComparison.Ordinal))
            {
                SetSentenceData((int)Sentence.HBT, strSentence);
            }

            Thread.Sleep(10);
        }

        SentenceInfoUpdated?.Invoke(channelName, _stIOSSentenceData);
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
