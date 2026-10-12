using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using POS_MT.Options;

namespace POS_MT.Services;

public enum PosWorkMode
{
    Auto,
    Online,
    Offline
}

public interface IPosConnectionRouter
{
    PosWorkMode WorkMode { get; }
    string ActiveTarget { get; } // Local | Online
    bool IsSqliteActive { get; }
    string GetOperationalConnectionString();
    string? GetLocalConnectionString();
    string? GetOnlineConnectionString();
    void SetWorkMode(PosWorkMode mode);
    Task RefreshAsync(CancellationToken ct = default);
    PosConnectionSnapshot Snapshot();
}

public sealed class PosConnectionSnapshot
{
    public string WorkMode { get; set; } = "Auto";
    public string ActiveTarget { get; set; } = "Online";
    public bool OnlineReachable { get; set; }
    public bool LocalConfigured { get; set; }
    public bool LocalReachable { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;
}

public sealed class PosConnectionRouter : IPosConnectionRouter
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _env;
    private readonly PosHubOptions _hubOptions;
    private readonly IOfflineSqlitePaths _sqlitePaths;
    private readonly ILogger<PosConnectionRouter> _logger;
    private readonly object _lock = new();
    private PosWorkMode _workMode = PosWorkMode.Auto;
    private bool _onlineReachable;
    private bool _localReachable;
    private string _activeTarget = "Online";
    private string _message = "Not checked yet";
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;

    public PosConnectionRouter(
        IConfiguration configuration,
        IWebHostEnvironment env,
        IOptions<PosHubOptions> hubOptions,
        IOfflineSqlitePaths sqlitePaths,
        ILogger<PosConnectionRouter> logger)
    {
        _configuration = configuration;
        _env = env;
        _hubOptions = hubOptions.Value;
        _sqlitePaths = sqlitePaths;
        _logger = logger;
        _workMode = LoadPersistedMode();
    }

    public PosWorkMode WorkMode
    {
        get { lock (_lock) return _workMode; }
    }

    public string ActiveTarget
    {
        get { lock (_lock) return _activeTarget; }
    }

    public bool IsSqliteActive
    {
        get { lock (_lock) return string.Equals(_activeTarget, "Local", StringComparison.OrdinalIgnoreCase); }
    }

    public void SetWorkMode(PosWorkMode mode)
    {
        lock (_lock)
        {
            _workMode = mode;
        }

        PersistMode(mode);
        _ = RefreshAsync();
    }

    public string? GetLocalConnectionString()
    {
        // Local offline store is always SQLite (not SQL Express).
        return _sqlitePaths.GetConnectionString();
    }

    public string? GetOnlineConnectionString()
    {
        var central = _configuration.GetConnectionString("POS_MT_Central");
        if (IsUsable(central)) return central;

        var primary = _configuration.GetConnectionString("POS_MT");
        if (IsUsable(primary) && !LooksLikeSqlite(primary!)) return primary;

        return IsUsable(central) ? central : null;
    }

    public string GetOperationalConnectionString()
    {
        if (DateTimeOffset.Now - _checkedAt > TimeSpan.FromSeconds(Math.Max(5, _hubOptions.StatusPollSeconds)))
        {
            try
            {
                RefreshAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Connection refresh failed.");
            }
        }

        lock (_lock)
        {
            if (_activeTarget == "Local")
            {
                return _sqlitePaths.GetConnectionString();
            }

            // Safety net: if we somehow stayed Online but SQLite is the only usable store, prefer it.
            if (_sqlitePaths.FileExists() && !_onlineReachable)
            {
                _activeTarget = "Local";
                _message = "Using local SQLite because online SQL is not reachable.";
                return _sqlitePaths.GetConnectionString();
            }

            var online = GetOnlineConnectionString()
                ?? _configuration.GetConnectionString("POS_MT")
                ?? throw new InvalidOperationException("No online database connection string is configured.");
            return online;
        }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        var localCs = GetLocalConnectionString();
        var onlineCs = GetOnlineConnectionString() ?? _configuration.GetConnectionString("POS_MT");

        var localConfigured = !string.IsNullOrWhiteSpace(localCs);
        var localOk = localConfigured && _sqlitePaths.FileExists() && await CanConnectSqliteAsync(localCs!, ct);
        var onlineOk = IsUsable(onlineCs) && !LooksLikeSqlite(onlineCs!) && await CanConnectSqlServerAsync(onlineCs!, 2, ct);

        PosWorkMode mode;
        lock (_lock) mode = _workMode;

        string target;
        string message;

        // Work mode controls which DB pages use (Online = central SQL, Offline = SQLite).
        // LocalHub only means sync worker is available — it must not ignore Mode: Online.
        if (mode == PosWorkMode.Offline)
        {
            if (localOk)
            {
                target = "Local";
                message = _hubOptions.IsLocalHub
                    ? "Forced Offline — using local SQLite (hub)."
                    : "Forced Offline — using local SQLite.";
            }
            else
            {
                target = "Online";
                message = localConfigured && !_sqlitePaths.FileExists()
                    ? "Offline requested but SQLite file is missing. Run: POS_MT.exe offline pull — staying Online."
                    : "Offline requested but local SQLite is not usable. Staying Online.";
                lock (_lock) _workMode = PosWorkMode.Auto;
                PersistMode(PosWorkMode.Auto);
                mode = PosWorkMode.Auto;
            }
        }
        else if (mode == PosWorkMode.Online)
        {
            if (onlineOk)
            {
                target = "Online";
                message = "Forced Online — using central/online SQL Server.";
            }
            else if (localOk)
            {
                // Never keep serving a dead Online target (causes 500s when Wi‑Fi is cut).
                target = "Local";
                message = "Online SQL unreachable — using local SQLite until central is back.";
            }
            else
            {
                target = "Online";
                message = "Forced Online but central SQL is unreachable and SQLite is missing. Run: POS_MT.exe offline pull";
            }
        }
        else
        {
            // Auto
            if (onlineOk)
            {
                target = "Online";
                message = "Auto — online SQL Server reachable.";
            }
            else if (localOk)
            {
                target = "Local";
                message = "Auto — online SQL unreachable; switched to local SQLite.";
            }
            else
            {
                target = "Online";
                message = "Auto — online SQL unreachable and SQLite offline DB is missing. Run: POS_MT.exe offline pull";
            }
        }

        lock (_lock)
        {
            _onlineReachable = onlineOk;
            _localReachable = localOk;
            _activeTarget = target;
            _message = message;
            _checkedAt = DateTimeOffset.Now;
        }

        _logger.LogInformation("DB routing: mode={Mode} active={Active} online={Online} sqlite={Local}",
            mode, target, onlineOk, localOk);
    }

    public PosConnectionSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new PosConnectionSnapshot
            {
                WorkMode = _workMode.ToString(),
                ActiveTarget = _activeTarget,
                OnlineReachable = _onlineReachable,
                LocalConfigured = !string.IsNullOrWhiteSpace(GetLocalConnectionString()),
                LocalReachable = _localReachable,
                Message = _message,
                CheckedAt = _checkedAt
            };
        }
    }

    private string ModeFilePath =>
        Path.Combine(_env.ContentRootPath, "App_Data", "pos-work-mode.json");

    private PosWorkMode LoadPersistedMode()
    {
        try
        {
            var path = ModeFilePath;
            if (!File.Exists(path)) return PosWorkMode.Auto;
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("mode", out var modeProp)
                && Enum.TryParse<PosWorkMode>(modeProp.GetString(), true, out var mode))
            {
                return mode;
            }
        }
        catch
        {
            /* ignore */
        }

        return PosWorkMode.Auto;
    }

    private void PersistMode(PosWorkMode mode)
    {
        try
        {
            var dir = Path.GetDirectoryName(ModeFilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { mode = mode.ToString(), updatedAt = DateTimeOffset.Now });
            File.WriteAllText(ModeFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not persist work mode.");
        }
    }

    private static bool IsUsable(string? cs) =>
        !string.IsNullOrWhiteSpace(cs)
        && !cs.Contains("YOUR_", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSqlite(string cs)
    {
        var s = cs.Trim();
        return s.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
               && (s.EndsWith(".db", StringComparison.OrdinalIgnoreCase)
                   || s.Contains(".db;", StringComparison.OrdinalIgnoreCase)
                   || s.Contains("Mode=", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<bool> CanConnectSqlServerAsync(string connectionString, int timeoutSeconds, CancellationToken ct)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString)
            {
                ConnectTimeout = timeoutSeconds
            };
            await using var conn = new SqlConnection(builder.ConnectionString);
            await conn.OpenAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> CanConnectSqliteAsync(string connectionString, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqliteConnection(connectionString);
            await conn.OpenAsync(ct);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1;";
            _ = await cmd.ExecuteScalarAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
