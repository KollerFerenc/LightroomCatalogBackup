using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Serilog;

namespace LightroomCatalogBackup
{
    class Program
    {
        private static readonly SHA256 _sha256 = SHA256.Create();
        private static readonly JsonSerializerOptions _jsonSerializerOptions = new() { WriteIndented = true };

        private static IBackupSettings _backupSettings;

        static void Main(string[] args)
        {
            ConfigureLogger();

            OpenSettings();
            BackupProcedure();

            Exit(0);
        }

        private static void ConfigureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(@"logs\log-.txt",
                    rollingInterval: RollingInterval.Year,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        private static void Exit(int exitCode)
        {
            Log.CloseAndFlush();
            Environment.Exit(exitCode);
        }

        private static void OpenSettings()
        {
            if (!File.Exists(@"config.json"))
            {
                Log.Fatal("config.json not found.");
                Exit(-1);
            }

            Log.Information("Loading config.json.");
            using (StreamReader reader = new(@"config.json"))
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

        private static void BackupProcedure()
        {
            Log.Information("Starting backup procedure.");

            foreach (var catalog in _backupSettings.Catalogs)
            {
                string destinationFile = "";

                if (catalog.HasCustomBackupDirectory)
                    destinationFile = Path.Combine(catalog.CustomBackupDirectory, catalog.FileName);
                else
                    destinationFile = Path.Combine(_backupSettings.GlobalBackupDirectory, catalog.FileName);

                if (File.Exists(destinationFile))
                {
                    if (GetHashSha256(destinationFile) == GetHashSha256(catalog.PathToFile))
                    {
                        Log.Information($"Skipping { catalog.FileName }, hash identical.");
                        // skip
                    }
                    else
                    {
                        var catalogInfo = new FileInfo(catalog.PathToFile);
                        var destinationInfo = new FileInfo(destinationFile);

                        if (catalogInfo.LastWriteTimeUtc.CompareTo(destinationInfo.LastWriteTimeUtc) >= 0)
                        {
                            catalogInfo = null;
                            destinationInfo = null;

                            try
                            {
                                Log.Information($"Copying { catalog.FileName }, newer.");
                                File.Copy(catalog.PathToFile, destinationFile, overwrite: true);
                            }
                            catch (Exception)
                            {
                                Log.Error($"Error while copying { catalog.FileName }.");
                            }
                        }
                        else
                        {
                            Log.Information($"Skipping { catalog.FileName }, older.");
                            // skip
                        }
                    }
                }
                else
                {
                    try
                    {
                        Log.Information($"Copying { catalog.FileName }, new.");
                        File.Copy(catalog.PathToFile, destinationFile);
                    }
                    catch (Exception)
                    {
                        Log.Error($"Error while copying { catalog.FileName }.");
                    }
                }
            }

            Log.Information("Backup finished.");
        }

        private static string GetHashSha256(string filename)
        {
            using (FileStream stream = File.OpenRead(filename))
            {
                return BitConverter.ToString(_sha256.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
            }
        }
    }
}
