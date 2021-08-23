using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CommandDotNet;
using Serilog;
using System.IO.Compression;

namespace LightroomCatalogBackup
{
    public class LightroomCatalogBackup
    {
        private static readonly SHA256 _sha256 = SHA256.Create();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private static readonly string _pathToConfig = Path.Combine(Program.baseDirectory, @"config.json");

        private bool _settingsImported;
        private IBackupSettings _backupSettings;

        [Command(
            Name = "init",
            Description = "Initialize config.")]
        public void Init(
            [Operand(
                Description = "Path to the global backup directory.")]
            string globalBackupDirectory,
            [Option(
                LongName = "compress",
                Description = "Compress catalogs.",
                BooleanMode = BooleanMode.Implicit)]
            bool compress,
            [Option(
                LongName = "force",
                ShortName = "f",
                Description="Overwrite existing configuration.",
                BooleanMode=BooleanMode.Implicit)]
            bool force)
        {
            Log.Information("Initialize config.");

            if (File.Exists(_pathToConfig))
            {
                if (!force)
                {
                    Log.Fatal("Config already exists.");
                    Program.Exit(-4);
                }
            }

            _backupSettings = new BackupSettings();
            _backupSettings.GlobalBackupDirectory = globalBackupDirectory;
            _backupSettings.Compress = compress;

            if (_backupSettings.Validate() == BackupSettingsValidationResult.InvalidGlobalBackupDirectory)
            {
                Log.Fatal("Invalid global backup directory.");
                Program.Exit(-6);
            }

            SaveConfig();
        }

        [Command(
            Name = "addCatalog",
            Description = "Add catalog to config.")]
        public void AddCatalog(
            [Operand(
                Description = "Path to catalog.")]
            string pathToCatalog)
        {
            if (!_settingsImported)
            {
                ImportSettings();
            }

            var catalog = new LightroomCatalog(pathToCatalog);

            if (!catalog.Validate())
            {
                Log.Fatal("Catalog not valid.");
                Program.Exit(-5);
            }

            Log.Information("Added catalog.");
            _backupSettings.Catalogs.Add(catalog);

            SaveConfig();
        }

        [Command(
            Name = "showConfig",
            Description = "Show current config.")]
        public void ShowConfig()
        {
            if (!_settingsImported)
            {
                ImportSettings();
            }

            Log.Debug("Writing config to console.");
            Console.WriteLine(JsonSerializer.Serialize(
                BackupSettings.GetBackupSettings(_backupSettings), _jsonSerializerOptions));
        }

        [Command(
            Name = "backup",
            Description = "Backup catalogs.")]
        public void Backup(
            [Option(
                LongName = "dryRun",
                Description= "Dry run of backup procedure.",
                BooleanMode=BooleanMode.Implicit)]
            bool dryRun)
        {
            if (!_settingsImported)
            {
                ImportSettings();
            }

            if (dryRun)
            {
                Log.Information("[DRY] Starting backup procedure.");
            }
            else
            {
                Log.Information("Starting backup procedure.");
            }

            foreach (var catalog in _backupSettings.Catalogs)
            {
                string destinationFile = GetDestinationFile(catalog);

                if (File.Exists(destinationFile))
                {
                    var hashResult = CompareHash(catalog.PathToFile, destinationFile);
                    if (hashResult == CompareResult.Zero)
                    {
                        if (dryRun)
                        {
                            Log.Information($"[DRY] Skipping { catalog.FileName }, hash identical.");
                        }
                        else
                        {
                            Log.Information($"Skipping { catalog.FileName }, hash identical.");
                        }
                        // skip
                    }
                    else if (hashResult == CompareResult.Negative)
                    {
                        var fileDateResult = CompareFileDate(catalog.PathToFile, destinationFile);
                        if (fileDateResult == CompareResult.Zero || fileDateResult == CompareResult.Positive)
                        {
                            BackupCatalog(catalog, destinationFile, dryRun);
                        }
                        else if (fileDateResult == CompareResult.Negative)
                        {
                            if (dryRun)
                            {
                                Log.Information($"[DRY] Skipping { catalog.FileName }, older.");
                            }
                            else
                            {
                                Log.Information($"Skipping { catalog.FileName }, older.");
                            }
                            // skip
                        }
                        else
                        {
                            Log.Information($"{ catalog.FileName } not found in zip archive.");
                            BackupCatalog(catalog, destinationFile, dryRun);
                        }
                    }
                    else
                    {
                        Log.Information($"{ catalog.FileName } not found in zip archive.");
                        BackupCatalog(catalog, destinationFile, dryRun);
                    }
                }
                else
                {
                    BackupCatalog(catalog, destinationFile, dryRun);
                }
            }

            if (dryRun)
            {
                Log.Information("[DRY] Backup finished.");
            }
            else
            {
                Log.Information("Backup finished.");
            }
        }

        [Command(
            Name = "sample",
            Description = "Show sample config.")]
        public void Sample(
            [Option(Description="Write sample to file.",
                BooleanMode=BooleanMode.Implicit)]
            bool toFile)
        {
            if (toFile)
            {
                string path = Path.Combine(Environment.CurrentDirectory, "sample_config-" + DateTime.Now.ToString("g").Replace(" ", "").Replace(":", "") + ".json");
                Log.Information($"Saving sample config to { path }.");

                using (StreamWriter writer = new StreamWriter(path))
                {
                    string json = JsonSerializer.Serialize(BackupSettings.GetSampleBackupSettings(), _jsonSerializerOptions);
                    writer.Write(json);
                }
            }
            else
            {
                Log.Debug("Writing sample config to console.");
                Console.WriteLine(JsonSerializer.Serialize(BackupSettings.GetSampleBackupSettings(), _jsonSerializerOptions));
            }
        }

        private void ImportSettings()
        {
            if (!File.Exists(_pathToConfig))
            {
                Log.Fatal("config.json not found.");
                Program.Exit(-1);
            }

            Log.Debug("Loading config.json.");
            using (StreamReader reader = new(_pathToConfig))
            {
                try
                {
                    _backupSettings = JsonSerializer.Deserialize<BackupSettings>(reader.ReadToEnd(), _jsonSerializerOptions);
                }
                catch (Exception)
                {
                    Log.Fatal("Could not load config.json.");
                    Program.Exit(-2);
                }
            }

            Log.Debug("Validating config.");
            if (_backupSettings.Validate() == BackupSettingsValidationResult.InvalidGlobalBackupDirectory)
            {
                Log.Fatal("Global backup directory does not exist.");
                Program.Exit(-3);
            }

            _settingsImported = true;
        }

        private void SaveConfig()
        {
            Log.Information("Saving config.");
            using (StreamWriter writer = new StreamWriter(_pathToConfig, append: false))
            {
                string json = JsonSerializer.Serialize(
                    BackupSettings.GetBackupSettings(_backupSettings), _jsonSerializerOptions);
                writer.Write(json);
            }
        }

        private string GetDestinationFile(ILightroomCatalog lightroomCatalog)
        {
            Log.Debug("Finding destination file.");

            if (_backupSettings.Compress)
            {
                if (lightroomCatalog.HasCustomBackupDirectory)
                    return Path.Combine(lightroomCatalog.CustomBackupDirectory, Path.ChangeExtension(lightroomCatalog.FileName, ".zip"));
                else
                    return Path.Combine(_backupSettings.GlobalBackupDirectory, Path.ChangeExtension(lightroomCatalog.FileName, ".zip"));
            }
            else if (lightroomCatalog.HasCustomBackupDirectory)
                return Path.Combine(lightroomCatalog.CustomBackupDirectory, lightroomCatalog.FileName);
            else
                return Path.Combine(_backupSettings.GlobalBackupDirectory, lightroomCatalog.FileName);
        }

        private CompareResult CompareHash(string catalog, string destination)
        {
            if (_backupSettings.Compress)
            {
                using ZipArchive archive = ZipFile.OpenRead(destination);
                foreach (var item in archive.Entries)
                {
                    if (item.Name == Path.GetFileName(catalog))
                    {
                        if (GetHashSha256(catalog) == GetHashSha256(item.Open()))
                        {
                            return CompareResult.Zero;
                        }
                        else
                        {
                            return CompareResult.Negative;
                        }
                    }
                }
            }
            else
            {
                if (GetHashSha256(catalog) == GetHashSha256(destination))
                {
                    return CompareResult.Zero;
                }
                else
                {
                    return CompareResult.Negative;
                }
            }

            return CompareResult.NotFound;
        }

        private CompareResult CompareFileDate(string catalog, string destination)
        {
            if (_backupSettings.Compress)
            {
                using ZipArchive archive = ZipFile.OpenRead(destination);
                foreach (var item in archive.Entries)
                {
                    if (item.Name == Path.GetFileName(catalog))
                    {
                        int result = new FileInfo(catalog).LastWriteTimeUtc.CompareTo(item.LastWriteTime.DateTime.ToUniversalTime());
                        if (result < 0)
                            return CompareResult.Negative;
                        else if (result == 0)
                            return CompareResult.Zero;
                        else
                            return CompareResult.Positive;
                    }
                }
            }
            else
            {
                var sourceInfo = new FileInfo(catalog);
                var destinationInfo = new FileInfo(destination);

                int result = sourceInfo.LastWriteTimeUtc.CompareTo(destinationInfo.LastWriteTimeUtc);
                if (result < 0)
                    return CompareResult.Negative;
                else if (result == 0)
                    return CompareResult.Zero;
                else
                    return CompareResult.Positive;
            }

            return CompareResult.NotFound;
        }

        private void BackupCatalog(ILightroomCatalog lightroomCatalog, string destination, bool dryRun)
        {
            if (_backupSettings.Compress)
            {
                if (dryRun)
                {
                    Log.Information($"[DRY] Creating zip archive for { lightroomCatalog.FileName }.");
                }
                else
                {
                    try
                    {
                        Log.Information($"Creating zip archive for { lightroomCatalog.FileName }.");

                        using var zipToCreate = new FileStream(destination, FileMode.Create);
                        using var archive = new ZipArchive(zipToCreate, ZipArchiveMode.Create);
                        archive.CreateEntryFromFile(lightroomCatalog.PathToFile, lightroomCatalog.FileName);
                    }
                    catch (Exception)
                    {
                        Log.Error($"Error while creating zip archive for { lightroomCatalog.FileName }.");
                    }
                }
            }
            else
            {
                if (dryRun)
                {
                    Log.Information($"[DRY] Copying { lightroomCatalog.FileName }.");
                }
                else
                {
                    try
                    {
                        Log.Information($"Copying { lightroomCatalog.FileName }.");
                        File.Copy(lightroomCatalog.PathToFile, destination);
                    }
                    catch (Exception)
                    {
                        Log.Error($"Error while copying { lightroomCatalog.FileName }.");
                    }
                }
            }
        }

        private static string GetHashSha256(string filename)
        {
            using FileStream stream = File.OpenRead(filename);
            return BitConverter.ToString(_sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        private static string GetHashSha256(Stream stream)
        {
            return BitConverter.ToString(_sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }

    public enum CompareResult
    {
        Negative,
        Zero,
        Positive,
        NotFound,
    }
}
