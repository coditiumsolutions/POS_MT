namespace POS_MT.ViewModels;

public sealed class InventoryBalancePrintViewModel
{
    public DateTime PrintedAt { get; set; }
    public IReadOnlyList<InventoryListItemViewModel> Rows { get; set; } = [];
}
