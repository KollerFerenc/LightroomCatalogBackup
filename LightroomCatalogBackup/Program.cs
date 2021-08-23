using System;
using System.IO;
using Serilog;
using CommandDotNet;

namespace LightroomCatalogBackup
{
    class Program
    { 
        internal static readonly string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

        static void Main(string[] args)
        {
            ConfigureLogger();

            var appRunner = new AppRunner<LightroomCatalogBackup>()
                .UseDefaultMiddleware();
            appRunner.Run(args);

            Exit(0);
        }

        private static void ConfigureLogger()
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(Path.Join(baseDirectory, @"logs\log-.txt"),
                    rollingInterval: RollingInterval.Year,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        internal static void Exit(int exitCode)
        {
            Log.CloseAndFlush();
            Environment.Exit(exitCode);
        }
    }
}
