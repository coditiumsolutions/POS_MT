using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class PurchaseInvoiceFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(50), Display(Name = "Purchase Invoice No")]
    public string PurchaseInvoiceNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vendor Invoice No is required."), StringLength(100), Display(Name = "Vendor Invoice No")]
    public string VendorInvoiceNo { get; set; } = string.Empty;

    [Display(Name = "Purchase Date")]
    public DateTime PurchaseDate { get; set; } = DateTime.Now;

    [Required(ErrorMessage = "Branch is required."), Display(Name = "Branch")]
    public int? BranchUid { get; set; }

    /// <summary>Resolved from Branch on save; not shown on the create form.</summary>
    [Display(Name = "Warehouse")]
    public int? WarehouseUid { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Vendor is required."), Display(Name = "Vendor")]
    public int VendorUid { get; set; }

    [Display(Name = "Sub Total")]
    public decimal SubTotal { get; set; }

    [Display(Name = "Discount")]
    public decimal DiscountAmount { get; set; }

    [Display(Name = "Tax")]
    public decimal TaxAmount { get; set; }

    [Display(Name = "Other Charges")]
    public decimal OtherCharges { get; set; }

    [Display(Name = "Net Amount")]
    public decimal NetAmount { get; set; }

    [Display(Name = "Paid Amount")]
    public decimal PaidAmount { get; set; }

    [Display(Name = "Balance")]
    public decimal BalanceAmount { get; set; }

    [StringLength(30), Display(Name = "Payment Status")]
    public string PaymentStatus { get; set; } = "Unpaid";

    [StringLength(30), Display(Name = "Invoice Status")]
    public string InvoiceStatus { get; set; } = "Completed";

    [StringLength(500)]
    public string? Remarks { get; set; }

    /// <summary>Receive lines — at least one required on create.</summary>
    public List<PurchaseInvoiceLineFormViewModel> Lines { get; set; } = [];
}

public sealed class PurchaseInvoiceLineFormViewModel
{
    [Display(Name = "Product")]
    public int? ProductUid { get; set; }

    [Display(Name = "Quantity")]
    public decimal Quantity { get; set; }

    [Display(Name = "Unit Price")]
    public decimal UnitCost { get; set; }
}

public sealed class PurchaseProductOptionViewModel
{
    public int Uid { get; set; }
    public string Label { get; set; } = string.Empty;
}
