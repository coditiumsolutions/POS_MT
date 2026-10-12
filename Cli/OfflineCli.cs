using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POS_MT.Options;
using POS_MT.Services;

namespace POS_MT.Cli;

public static class OfflineCli
{
    public static async Task<int> RunAsync(string[] args)
    {
        var command = args.Length > 0 ? args[0] : "help";
        if (string.Equals(command, "help", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "-h", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "--help", StringComparison.OrdinalIgnoreCase))
        {
            PrintHelp();
            return 0;
        }

        var contentRoot = Directory.GetCurrentDirectory();
        // Prefer Development secrets for local CLI sync when that file exists.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"))
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"))
            && File.Exists(Path.Combine(contentRoot, "appsettings.Development.json")))
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            Environment.SetEnvironmentVariable("DOTNET_ENVIRONMENT", "Development");
        }

        var host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((ctx, config) =>
            {
                config.SetBasePath(contentRoot);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
                config.AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false);
                config.AddJsonFile($"appsettings.{ctx.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: false);
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((ctx, services) =>
            {
                services.AddSingleton<IWebHostEnvironment>(new CliWebHostEnvironment(contentRoot, ctx.HostingEnvironment.EnvironmentName));
                services.Configure<PosHubOptions>(ctx.Configuration.GetSection(PosHubOptions.SectionName));
                services.AddSingleton<IOfflineSqlitePaths, OfflineSqlitePaths>();
                services.AddSingleton<IPosConnectionRouter, PosConnectionRouter>();
                services.AddScoped<IOfflineSqliteSyncService, OfflineSqliteSyncService>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddConsole();
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        using var scope = host.Services.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<IOfflineSqliteSyncService>();

        try
        {
            var message = command.ToLowerInvariant() switch
            {
                "init" => await sync.InitAsync(),
                "pull" or "configure" or "sync-down" => await sync.PullAsync(),
                "push" or "sync-up" => await sync.PushAsync(),
                "status" => await sync.StatusAsync(),
                "sync" => await FullSyncAsync(sync),
                _ => throw new InvalidOperationException($"Unknown offline command '{command}'.")
            };

            Console.WriteLine(message);
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("ERROR: " + ex.Message);
            return 1;
        }
    }

    private static async Task<string> FullSyncAsync(IOfflineSqliteSyncService sync)
    {
        var pull = await sync.PullAsync();
        var push = await sync.PushAsync();
        return pull + Environment.NewLine + push;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("""
POS_MT offline SQLite commands

  dotnet run -- offline init       Create empty SQLite schema (App_Data/pos-offline.db)
  dotnet run -- offline pull       Download catalog from central SQL Server into SQLite
  dotnet run -- offline push       Upload pending offline sales (SyncOutbox) to central SQL
  dotnet run -- offline sync       pull + push
  dotnet run -- offline status     Show SQLite path and row counts

Published:
  POS_MT.exe offline pull
  POS_MT.exe offline push
  POS_MT.exe offline status

Configure:
  - ConnectionStrings:POS_MT (or POS_MT_Central) = central SQL Server
  - PosHub:OfflineSqlitePath = App_Data/pos-offline.db (optional)
  - Or ConnectionStrings:POS_MT_Offline = Data Source=App_Data/pos-offline.db

Then in the web UI click Offline (uses the SQLite file on this machine).
""");
    }

    private sealed class CliWebHostEnvironment : IWebHostEnvironment
    {
        public CliWebHostEnvironment(string contentRoot, string environmentName)
        {
            ContentRootPath = contentRoot;
            EnvironmentName = string.IsNullOrWhiteSpace(environmentName) ? "Production" : environmentName;
            ApplicationName = "POS_MT";
            WebRootPath = Path.Combine(contentRoot, "wwwroot");
            ContentRootFileProvider = new PhysicalFileProvider(contentRoot);
            WebRootFileProvider = Directory.Exists(WebRootPath)
                ? new PhysicalFileProvider(WebRootPath)
                : new NullFileProvider();
        }

        public string ApplicationName { get; set; }
        public IFileProvider ContentRootFileProvider { get; set; }
        public string ContentRootPath { get; set; }
        public string EnvironmentName { get; set; }
        public string WebRootPath { get; set; }
        public IFileProvider WebRootFileProvider { get; set; }
    }
}
