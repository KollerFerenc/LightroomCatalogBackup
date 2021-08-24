using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace LightroomCatalogBackup
{
    public class LightroomCatalog : ILightroomCatalog, IEquatable<LightroomCatalog>
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

        public override string ToString()
        {
            return FileName;
        }

        public bool Equals(ILightroomCatalog other)
        {
            if (other is null)
            {
                return false;
            }

            return (this.PathToFile == other.PathToFile
                && this.CustomBackupDirectory == other.CustomBackupDirectory);
        }

        public bool Equals(LightroomCatalog other)
        {
            return Equals((ILightroomCatalog)other);
        }

        public override bool Equals(object obj)
        {
            return obj is LightroomCatalog objS && Equals(objS);
        }

        public override int GetHashCode()
        {
            return Tuple.Create(PathToFile, CustomBackupDirectory).GetHashCode();
        }

        public static bool operator ==(LightroomCatalog catalog1, LightroomCatalog catalog2)
        {
            if ((object)catalog1 == null || ((object)catalog2) == null)
            {
                return System.Object.Equals(catalog1, catalog2);
            }

            return catalog1.Equals(catalog2);
        }

        public static bool operator !=(LightroomCatalog catalog1, LightroomCatalog catalog2)
        {
            return !(catalog1 == catalog2);
        }
    }
}
