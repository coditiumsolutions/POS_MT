using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class YogurtProductionViewModel
{
    /// <summary>Fresh Yogurt quantity to produce (kg).</summary>
    [Required]
    [Range(0.001, 100000, ErrorMessage = "Yogurt quantity must be greater than zero.")]
    [Display(Name = "Fresh Yogurt (kg)")]
    public decimal YogurtQuantity { get; set; }

    [Display(Name = "Milk required (kg)")]
    public decimal MilkQuantity { get; set; }

    public int? MilkProductUid { get; set; }
    public string? MilkProductName { get; set; }
    public decimal MilkAvailable { get; set; }

    public int? YogurtProductUid { get; set; }
    public string? YogurtProductName { get; set; }
    public decimal YogurtAvailable { get; set; }

    public string? BranchName { get; set; }
    public string? WarehouseHint { get; set; }

    /// <summary>Milk kg needed per 1 kg yogurt (40/32).</summary>
    public decimal MilkPerYogurtKg { get; set; } = 40m / 32m;
}

public sealed class YogurtProductionResultViewModel
{
    public string InvoiceNo { get; set; } = string.Empty;
    public int InvoiceUid { get; set; }
    public decimal YogurtQuantity { get; set; }
    public decimal MilkQuantity { get; set; }
}
