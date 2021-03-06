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

        public static BackupSettings GetSampleBackupSettings()
        {
            var output = new BackupSettings();

            try
            {
                output.Compress = true;
                output.GlobalBackupDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                output.Catalogs.Add(new LightroomCatalog()
                {
                    PathToFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Lightroom", "Lightroom Catalog.lrcat"),
                });
            }
            catch (Exception)
            {
                return new BackupSettings();
            }
            

            return output;
        }

        public static BackupSettings GetBackupSettings(IBackupSettings backupSettings)
        {
            BackupSettings output = new();

            output.GlobalBackupDirectory = backupSettings.GlobalBackupDirectory;
            output.Compress = backupSettings.Compress;
            output.Catalogs = backupSettings.Catalogs;

            return output;
        }
    }
}
