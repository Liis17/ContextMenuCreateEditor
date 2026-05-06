namespace ContextMenuCreateEditor.WPF.Services
{
    public interface IRegistryBackupService
    {
        string BackupsFolder { get; }
        string CreateBackup();
    }
}
