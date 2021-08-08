namespace LightroomCatalogBackup
{
    public interface ILightroomCatalog
    {
        string CustomBackupDirectory { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        string FileName { get; }
        [System.Text.Json.Serialization.JsonIgnore]
        bool HasCustomBackupDirectory { get; }
        string PathToFile { get; set; }

        bool Validate();
    }
}