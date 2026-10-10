namespace POS_MT.ViewModels;

public sealed class PosProductItemViewModel
{
    public int Uid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public decimal SalePrice { get; set; }
    public int? CategoryUid { get; set; }
}

public sealed class PosCategoryItemViewModel
{
    public int Uid { get; set; }
    public string CategoryName { get; set; } = string.Empty;
}

public sealed class PosSaleViewModel
{
    public string Mode { get; set; } = "Cash";
    public List<PosProductItemViewModel> Products { get; set; } = [];
    public List<PosCategoryItemViewModel> Categories { get; set; } = [];
}
