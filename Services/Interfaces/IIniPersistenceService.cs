namespace NMEAReceiver.Services.Interfaces;

public interface IIniPersistenceService
{
    IniSettings Load();
    void Save(IniSettings settings);
}
