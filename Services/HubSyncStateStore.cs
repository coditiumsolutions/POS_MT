namespace POS_MT.Services;

/// <summary>Process-wide sync counters for the hub status badge.</summary>
public sealed class HubSyncStateStore
{
    private readonly object _lock = new();
    private string _syncStatus = "Idle";
    private int _pendingCount;
    private int _failedCount;

    public void Report(string syncStatus, int pendingCount, int failedCount)
    {
        lock (_lock)
        {
            _syncStatus = syncStatus;
            _pendingCount = pendingCount;
            _failedCount = failedCount;
        }
    }

    public (string SyncStatus, int PendingCount, int FailedCount) Snapshot()
    {
        lock (_lock)
        {
            return (_syncStatus, _pendingCount, _failedCount);
        }
    }
}
