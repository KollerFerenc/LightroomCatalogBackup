using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LightroomCatalogBackup
{
    public class BackupSettings : IBackupSettings
    {
        public string GlobalBackupDirectory { get; set; }
        public bool Compress { get; set; } = false;
        public List<LightroomCatalog> Catalogs { get; set; } = new(4);

        public BackupSettings()
        {

        }

        public BackupSettings(string globalBackupDirectory)
        {
            GlobalBackupDirectory = globalBackupDirectory;
        }

        public BackupSettingsValidationResult Validate()
        {
            if (!Directory.Exists(GlobalBackupDirectory))
            {
                return BackupSettingsValidationResult.InvalidGlobalBackupDirectory;
            }

            var result = BackupSettingsValidationResult.Valid;

            foreach (var catalog in Catalogs)
            {
                if (!catalog.Validate())
                {
                    Log.Warning($"Validation failed. Removing { catalog.FileName } from list.");
                    Catalogs.Remove(catalog);
                    result = BackupSettingsValidationResult.InvalidLightroomCatalog;
                }
            }

            return result;
        }
    }

    public enum BackupSettingsValidationResult
    {
        Valid,
        InvalidGlobalBackupDirectory,
        InvalidLightroomCatalog,
    }
}
