using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class CustomerPaymentFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(50), Display(Name = "Payment No")]
    public string PaymentNo { get; set; } = string.Empty;

    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    [Required, Display(Name = "Customer")]
    public int CustomerUid { get; set; }

    /// <summary>Advance = prepaid credit; AgainstInvoice = apply to a sales invoice (excess still becomes advance).</summary>
    [Display(Name = "Payment Type")]
    public string PaymentType { get; set; } = "Advance";

    [Display(Name = "Sales Invoice")]
    public int? SalesInvoiceUid { get; set; }

    [Display(Name = "Payment Method")]
    public int? PaymentMethodUid { get; set; }

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(100), Display(Name = "Reference No")]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    public decimal CurrentAdvanceBalance { get; set; }
}

public sealed class CustomerPaymentListItemViewModel
{
    public int Uid { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string? InvoiceNo { get; set; }
    public decimal Amount { get; set; }
    public decimal AppliedToInvoiceAmount { get; set; }
    public decimal AdvanceAmount => Math.Max(0, Amount - AppliedToInvoiceAmount);
}
