using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LightroomCatalogBackup
{
    public class LightroomCatalog : ILightroomCatalog
    {
        public string PathToFile { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public string FileName => Path.GetFileName(PathToFile);
        public string CustomBackupDirectory { get; set; } = "";
        [System.Text.Json.Serialization.JsonIgnore]
        public bool HasCustomBackupDirectory => !string.IsNullOrWhiteSpace(CustomBackupDirectory);

        public LightroomCatalog()
        {

        }

        public LightroomCatalog(string pathToFile)
        {
            PathToFile = pathToFile;
        }

        public bool Validate()
        {
            if (!File.Exists(PathToFile))
                return false;

            if (HasCustomBackupDirectory && !Directory.Exists(CustomBackupDirectory))
                return false;

            return true;
        }
    }
}
