namespace POS_MT.ViewModels;

public sealed class OfflineSyncTablesPageViewModel
{
    public List<OfflineSyncTableRowViewModel> Rows { get; set; } = [];
}

public sealed class OfflineSyncTableRowViewModel
{
    public int Uid { get; set; }
    public string TableName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool PullFromOnline { get; set; }
    public bool PushToOnline { get; set; }
    public string SyncMode { get; set; } = "Lookup";
    public int SortOrder { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime UpdatedDate { get; set; }
}
