using System.Runtime.InteropServices;

namespace NMEAReceiver.Models;

public enum Sentence
{
    HTD,
    RSA,
    ROR,
    PYDKN,
    ALF,
    ALC,
    ARC,
    HBT,
    ACN
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_HTD_SENTENCE
{
    [MarshalAs(UnmanagedType.U2)] public char szOverride;
    public double dRudderAngle;
    public int nRudderDirenction;
    public int nSteeringMode;
    [MarshalAs(UnmanagedType.U2)] public char szTurnMode;
    public double dCommandedRudderLimit;
    public double dCommandedOffHeadingLimit;
    public double dRadiusofTurnHeadingChanges;
    public double dRateofTurnHeadingChanges;
    public double dCommandedHeadingSteer;
    public double dCommandedOffTrackLimit;
    public double dCommandedTrack;
    [MarshalAs(UnmanagedType.U2)] public char szHeadingReferenceUse;
    [MarshalAs(UnmanagedType.U2)] public char szRudderStatus;
    [MarshalAs(UnmanagedType.U2)] public char szOffHeadingStatus;
    [MarshalAs(UnmanagedType.U2)] public char szOffTrackStatus;
    public double dVesselHeading;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_RSA_SENTENCE
{
    public double dStarboardRudderSensor;
    public double dPortRudderSensor;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_ROR_SENTENCE
{
    public double dStarboardRudderOrder;
    public double dPortRudderOrder;
    [MarshalAs(UnmanagedType.U2)] public char szCommandedSourceLocation;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_PYDKN_SENTENCE
{
    [MarshalAs(UnmanagedType.U2)] public char szNFCommand;
    public int nSteeringMode;
    public int nSTBDRudderCommnad;
    public int nPORTRudderCommand;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_ALF_SENTENCE
{
    public int nTotalALFSentence;
    public int nSentenceNumber;
    public int nSequentialMessage;
    [MarshalAs(UnmanagedType.U2)] public char szTimeLastChange;
    [MarshalAs(UnmanagedType.U2)] public char szAlertCategory;
    [MarshalAs(UnmanagedType.U2)] public char szAlertPriority;
    [MarshalAs(UnmanagedType.U2)] public char szAlertState;
    [MarshalAs(UnmanagedType.U2)] public char szManufacturerMnemonicCode;
    public double dAlertIdentifier;
    public double dAlertInstance;
    public double dRevisionCounter;
    public int nEscalationcounter;
    [MarshalAs(UnmanagedType.U2)] public char szAlertText;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_ALC_SENTENCE
{
    public int nTotalALFSentence;
    public int nSentenceNumber;
    public int nSequentialMessage;
    public double dNumberAlertEntries;
    [MarshalAs(UnmanagedType.U2)] public char szManufacturerMnemonicCode;
    public double dAlertIdentifier;
    public double dAlertInstance;
    public double dRevisionCounter;
    [MarshalAs(UnmanagedType.U2)] public char szAdditionalA0lertEntries;
    [MarshalAs(UnmanagedType.U2)] public char szAlertEntry;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_ARC_SENTENCE
{
    [MarshalAs(UnmanagedType.U2)] public char szTime;
    [MarshalAs(UnmanagedType.U2)] public char szManufacturerMnemonicCode;
    [MarshalAs(UnmanagedType.U2)] public char szAlertIdentifier;
    public double dAlertInstance;
    [MarshalAs(UnmanagedType.U2)] public char szAlertCommand;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_ACN_SENTENCE
{
    [MarshalAs(UnmanagedType.U2)] public char szTime;
    [MarshalAs(UnmanagedType.U2)] public char szManufacturerMnemonicCode;
    public double dAlertIdentifier;
    public double dAlertInstance;
    [MarshalAs(UnmanagedType.U2)] public char szAlertCommand;
    [MarshalAs(UnmanagedType.U2)] public char szSentenceStatusFlag;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_HBT_SENTENCE
{
    public int nConfiguredRepeatInterval;
    [MarshalAs(UnmanagedType.U2)] public char szEquipmentStatus;
    public int nSequentialSentenceIdentifier;
}

[StructLayout(LayoutKind.Sequential, Pack = 8, CharSet = CharSet.Unicode)]
public struct ST_IOSSEND_SENTENCE
{
    public ST_HTD_SENTENCE m_stSentenceHTD;
    public ST_RSA_SENTENCE m_stSentenceRSA;
    public ST_ROR_SENTENCE m_stSentenceROR;
    public ST_PYDKN_SENTENCE m_stSentencePYDKN;
    public ST_ALF_SENTENCE m_stSentenceALF;
    public ST_ALC_SENTENCE m_stSentenceALC;
    public ST_ARC_SENTENCE m_stSentenceARC;
    public ST_ACN_SENTENCE m_stSentenceACN;
    public ST_HBT_SENTENCE m_stSentenceHBT;
}
