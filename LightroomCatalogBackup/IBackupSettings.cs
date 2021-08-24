using System.Collections.Generic;

namespace LightroomCatalogBackup
{
    public interface IBackupSettings
    {
        List<LightroomCatalog> Catalogs { get; set; }
        string GlobalBackupDirectory { get; set; }
        bool Compress { get; set; }
    }
}