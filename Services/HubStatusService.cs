using Microsoft.Extensions.Options;
using POS_MT.Options;

namespace POS_MT.Services;

public sealed class HubStatusSnapshot
{
    public string Mode { get; set; } = "Standard";
    public string ShopName { get; set; } = "Shop";
    public string WorkMode { get; set; } = "Auto";
    public string ActiveDatabase { get; set; } = "Online";
    public bool LocalDatabaseOk { get; set; }
    public bool LocalConfigured { get; set; }
    public string LocalDatabaseMessage { get; set; } = string.Empty;
    public bool CentralConfigured { get; set; }
    public bool CentralReachable { get; set; }
    public string CentralMessage { get; set; } = string.Empty;
    public string Connectivity { get; set; } = "Online";
    public string SyncStatus { get; set; } = "Idle";
    public int PendingCount { get; set; }
    public int FailedCount { get; set; }
    public string RoutingMessage { get; set; } = string.Empty;
    public DateTimeOffset CheckedAt { get; set; } = DateTimeOffset.Now;
}

public interface IHubStatusService
{
    Task<HubStatusSnapshot> GetStatusAsync(CancellationToken ct = default);
}

public sealed class HubStatusService : IHubStatusService
{
    private readonly PosHubOptions _options;
    private readonly HubSyncStateStore _syncState;
    private readonly IPosConnectionRouter _router;

    public HubStatusService(
        IOptions<PosHubOptions> options,
        HubSyncStateStore syncState,
        IPosConnectionRouter router)
    {
        _options = options.Value;
        _syncState = syncState;
        _router = router;
    }

    public async Task<HubStatusSnapshot> GetStatusAsync(CancellationToken ct = default)
    {
        await _router.RefreshAsync(ct);
        var route = _router.Snapshot();
        var (syncStatus, pending, failed) = _syncState.Snapshot();

        var snapshot = new HubStatusSnapshot
        {
            Mode = _options.Mode,
            ShopName = _options.ShopName,
            WorkMode = route.WorkMode,
            ActiveDatabase = route.ActiveTarget,
            LocalConfigured = route.LocalConfigured,
            LocalDatabaseOk = route.LocalReachable,
            LocalDatabaseMessage = route.LocalReachable
                ? "Local SQLite offline DB ready"
                : "Local SQLite missing — run: POS_MT.exe offline pull",
            CentralConfigured = !string.IsNullOrWhiteSpace(_router.GetOnlineConnectionString()),
            CentralReachable = route.OnlineReachable,
            CentralMessage = route.OnlineReachable
                ? "Online/central SQL reachable"
                : "Online/central SQL unreachable",
            RoutingMessage = route.Message,
            SyncStatus = syncStatus,
            PendingCount = pending,
            FailedCount = failed,
            CheckedAt = route.CheckedAt
        };

        if (string.Equals(route.ActiveTarget, "Local", StringComparison.OrdinalIgnoreCase))
        {
            snapshot.Connectivity = "Offline";
            if (failed > 0) snapshot.SyncStatus = "Sync Failed";
            else if (pending > 0) snapshot.SyncStatus = "Pending";
            else if (string.Equals(syncStatus, "Syncing", StringComparison.OrdinalIgnoreCase))
                snapshot.SyncStatus = "Syncing";
            else
                snapshot.SyncStatus = "Idle";
        }
        else
        {
            // Mode Online writes straight to central — outbox pending on SQLite must not show as Pending.
            snapshot.Connectivity = route.OnlineReachable ? "Online" : "Offline";
            snapshot.PendingCount = 0;
            snapshot.FailedCount = 0;
            if (!_options.IsLocalHub && !route.LocalReachable)
                snapshot.SyncStatus = "N/A";
            else
                snapshot.SyncStatus = route.OnlineReachable ? "Synced" : "Offline";
        }

        return snapshot;
    }
}
