using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class InventoryReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string SaleType { get; set; } = "All";
    public string ItemSearch { get; set; } = string.Empty;
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public IEnumerable<SelectListItem> SaleTypes { get; set; } = [];
    public List<InventoryReportRowViewModel> Rows { get; set; } = [];

    public int ItemCount => Rows.Count;
    public decimal TotalQuantity => Rows.Sum(x => x.QuantitySold);
    public decimal TotalAmount => Rows.Sum(x => x.Amount);
}

public sealed class InventoryReportRowViewModel
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Unit { get; set; } = "—";
    public decimal QuantitySold { get; set; }
    public decimal Amount { get; set; }

    public string SoldSummary =>
        string.IsNullOrWhiteSpace(Unit) || Unit == "—"
            ? $"{QuantitySold:N3}"
            : $"{QuantitySold:N3} {Unit}";
}
