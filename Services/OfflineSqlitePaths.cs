using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using POS_MT.Options;

namespace POS_MT.Services;

public interface IOfflineSqlitePaths
{
    string ResolveFilePath();
    string GetConnectionString();
    bool FileExists();
}

public sealed class OfflineSqlitePaths : IOfflineSqlitePaths
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly PosHubOptions _hubOptions;

    public OfflineSqlitePaths(
        IConfiguration configuration,
        IWebHostEnvironment env,
        IOptions<PosHubOptions> hubOptions)
    {
        _configuration = configuration;
        _env = env;
        _hubOptions = hubOptions.Value;
    }

    public string ResolveFilePath()
    {
        var fromCs = _configuration.GetConnectionString("POS_MT_Offline");
        if (!string.IsNullOrWhiteSpace(fromCs) && !fromCs.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            var builder = new SqliteConnectionStringBuilder(fromCs);
            return ResolvePath(builder.DataSource);
        }

        var configured = string.IsNullOrWhiteSpace(_hubOptions.OfflineSqlitePath)
            ? "App_Data/pos-offline.db"
            : _hubOptions.OfflineSqlitePath;
        return ResolvePath(configured);
    }

    public string GetConnectionString()
    {
        var path = ResolveFilePath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        return new SqliteConnectionStringBuilder { DataSource = path }.ConnectionString;
    }

    public bool FileExists() => File.Exists(ResolveFilePath());

    private string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "App_Data/pos-offline.db";
        }

        path = path.Trim().Trim('"');
        if (Path.IsPathRooted(path))
        {
            return Path.GetFullPath(path);
        }

        return Path.GetFullPath(Path.Combine(_env.ContentRootPath, path));
    }
}
