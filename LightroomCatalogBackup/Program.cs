using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Serilog;

namespace LightroomCatalogBackup
{
    class Program
    {
        private static readonly SHA256 _sha256 = SHA256.Create();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };
        private static readonly string _baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        private static IBackupSettings _backupSettings;

        static void Main(string[] args)
        {
            ConfigureLogger();

            if (args.Length == 1 && args[0].ToUpper() == "--SAMPLE")
            {
                Log.Information("Saving sample config.");
                string path = Path.Combine(Environment.CurrentDirectory, "sample_config-" + DateTime.Now.ToString("s") + ".json");
                Log.Information($"Sample config: { path }");

                using (StreamWriter writer = new StreamWriter(path))
                {
                    string json = JsonSerializer.Serialize(BackupSettings.GetSampleBackupSettings(), _jsonSerializerOptions);
                    writer.Write(json);
                }

                Exit(0);
            }

            ImportSettings();
            StartBackupProcedure();

            Exit(0);
        }

        private static void ConfigureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Join(_baseDirectory, @"logs\log-.txt"),
                    rollingInterval: RollingInterval.Year,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private static void Exit(int exitCode)
        {
            Log.CloseAndFlush();
            Environment.Exit(exitCode);
        }

        private static void ImportSettings()
        {
            string pathToConfig = Path.Join(_baseDirectory, @"config.json");

            if (!File.Exists(pathToConfig))
            {
                Log.Fatal("config.json not found.");
                Exit(-1);
            }

            Log.Information("Loading config.json.");
            using (StreamReader reader = new(pathToConfig))
            {
                try
                {
                    _backupSettings = JsonSerializer.Deserialize<BackupSettings>(reader.ReadToEnd(), _jsonSerializerOptions);
                }
                catch (Exception)
                {
                    Log.Fatal("Could not load config.json.");
                    Exit(-2);
                }                
            }

            Log.Information("Validating config.");
            if (_backupSettings.Validate() == BackupSettingsValidationResult.InvalidGlobalBackupDirectory)
            {
                Log.Fatal("Global backup directory does not exist.");
                Exit(-3);
            }
        }

        private static void StartBackupProcedure()
        {
            Log.Information("Starting backup procedure.");

            foreach (var catalog in _backupSettings.Catalogs)
            {
                string destinationFile = GetDestinationFile(catalog);

                if (File.Exists(destinationFile))
                {
                    var hashResult = CompareHash(catalog.PathToFile, destinationFile);
                    if (hashResult == CompareResult.Zero)
                    {
                        Log.Information($"Skipping { catalog.FileName }, hash identical.");
                        // skip
                    }
                    else if (hashResult == CompareResult.Negative)
                    {
                        var fileDateResult = CompareFileDate(catalog.PathToFile, destinationFile);
                        if (fileDateResult == CompareResult.Zero || fileDateResult == CompareResult.Positive)
                        {
                            BackupCatalog(catalog, destinationFile);
                        }
                        else if (fileDateResult == CompareResult.Negative)
                        {
                            Log.Information($"Skipping { catalog.FileName }, older.");
                            // skip
                        }
                        else
                        {
                            Log.Information($"{ catalog.FileName } not found in zip archive.");
                            BackupCatalog(catalog, destinationFile);
                        }
                    }
                    else
                    {
                        Log.Information($"{ catalog.FileName } not found in zip archive.");
                        BackupCatalog(catalog, destinationFile);
                    }
                }
                else
                {
                    BackupCatalog(catalog, destinationFile);
                }
            }

            Log.Information("Backup finished.");
        }

        private static string GetDestinationFile(ILightroomCatalog lightroomCatalog)
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

        private static CompareResult CompareHash(string catalog, string destination)
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

        private static CompareResult CompareFileDate(string catalog, string destination)
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

        private static void BackupCatalog(ILightroomCatalog lightroomCatalog, string destination)
        {
            if (_backupSettings.Compress)
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
