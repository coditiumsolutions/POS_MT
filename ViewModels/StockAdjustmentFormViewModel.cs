using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class StockAdjustmentFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(50), Display(Name = "Adjustment No")]
    public string AdjustmentNo { get; set; } = string.Empty;

    [Display(Name = "Adjustment Date")]
    public DateTime AdjustmentDate { get; set; } = DateTime.Now;

    [Display(Name = "Branch")]
    public int? BranchUid { get; set; }

    [Required, Display(Name = "Warehouse")]
    public int WarehouseUid { get; set; }

    [Required, StringLength(50), Display(Name = "Adjustment Type")]
    public string AdjustmentType { get; set; } = "Correction";

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Completed";

    [Required, Display(Name = "Product")]
    public int ProductUid { get; set; }

    [Range(0.001, double.MaxValue, ErrorMessage = "Quantity must be greater than zero.")]
    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    /// <summary>Increase or Decrease.</summary>
    [Required, Display(Name = "Direction")]
    public string Direction { get; set; } = "Increase";
}
