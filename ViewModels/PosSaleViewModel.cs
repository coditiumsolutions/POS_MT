namespace POS_MT.ViewModels;

public sealed class PosProductItemViewModel
{
    public int Uid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public decimal StockQuantity { get; set; }
    public int? CategoryUid { get; set; }
    public string? CategoryName { get; set; }
    public string? ImagePath { get; set; }
    public string? UnitName { get; set; }
    public string? UnitCode { get; set; }

    /// <summary>Sale price is for one dozen; POS qty is in pieces (12 = one dozen).</summary>
    public bool PricedByDozen =>
        (!string.IsNullOrWhiteSpace(UnitName) && UnitName.Contains("dozen", StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrWhiteSpace(UnitCode) && UnitCode.Contains("doz", StringComparison.OrdinalIgnoreCase));
}

public sealed class PosCategoryItemViewModel
{
    public int Uid { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public sealed class PosSaleViewModel
{
    public string Mode { get; set; } = "Cash";
    public string? PageTitle { get; set; }
    public bool UseCardLayout { get; set; }
    /// <summary>Interface-II: draggable splitter between items and basket panels.</summary>
    public bool EnablePanelToggle { get; set; }

    /// <summary>Shows a barcode scan field that adds items to the basket on Enter.</summary>
    public bool EnableBarcodeScan { get; set; }

    /// <summary>Sum of today's completed sales (NetAmount), excluding cancelled / return / wastage.</summary>
    public decimal TodaySaleAmount { get; set; }

    public List<PosProductItemViewModel> Products { get; set; } = [];
    public List<PosCategoryItemViewModel> Categories { get; set; } = [];
}
