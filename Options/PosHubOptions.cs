namespace POS_MT.Options;

public sealed class PosHubOptions
{
    public const string SectionName = "PosHub";

    /// <summary>
    /// Standard = online SQL Server by default; Offline switches to local SQLite.
    /// LocalHub = always use local SQLite; sync to central when reachable.
    /// </summary>
    public string Mode { get; set; } = "Standard";

    public string ShopName { get; set; } = "Shop";

    /// <summary>How often (seconds) the UI polls hub status.</summary>
    public int StatusPollSeconds { get; set; } = 15;

    /// <summary>How often (seconds) the sync worker runs when offline SQLite sync is enabled.</summary>
    public int SyncIntervalSeconds { get; set; } = 30;

    public bool SyncEnabled { get; set; } = true;

    /// <summary>
    /// Relative or absolute path to the offline SQLite file.
    /// Default: App_Data/pos-offline.db under the app content root.
    /// </summary>
    public string OfflineSqlitePath { get; set; } = "App_Data/pos-offline.db";

    public bool IsLocalHub =>
        string.Equals(Mode, "LocalHub", StringComparison.OrdinalIgnoreCase);
}
