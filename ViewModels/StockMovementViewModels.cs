using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public static class StockMovementClasses
{
    public const string FastMoving = "Fast-Moving";
    public const string SlowMoving = "Slow-Moving";
    public const string DeadStock = "Dead Stock";
}

public sealed class StockMovementRowViewModel
{
    public int ProductUid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal CurrentStock { get; set; }
    public decimal EstimatedStockValue { get; set; }
    public DateTime? LastSaleDate { get; set; }
    public int? DaysSinceLastSale { get; set; }
    public DateTime? LastPurchaseDate { get; set; }
    public decimal RecentSalesQty { get; set; }
    public decimal PriorSalesQty { get; set; }
    public string SalesTrend { get; set; } = "—";
    public string Classification { get; set; } = StockMovementClasses.FastMoving;
    public bool IsExcessInventory { get; set; }
}

public sealed class StockMovementIndexViewModel
{
    public string? Classification { get; set; }
    public IEnumerable<SelectListItem> ClassificationOptions { get; set; } = [];
    public int FastMovingMaxDaysSinceSale { get; set; }
    public int SlowMovingMinDaysSinceSale { get; set; }
    public int DeadStockMinDaysSinceSale { get; set; }
    public int TrendWindowDays { get; set; }
    public int FastMovingCount { get; set; }
    public int SlowMovingCount { get; set; }
    public int DeadStockCount { get; set; }
    public decimal TotalStockValue { get; set; }
    public List<StockMovementRowViewModel> Rows { get; set; } = [];
}
