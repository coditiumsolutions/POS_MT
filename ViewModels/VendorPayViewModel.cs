using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class VendorPayViewModel
{
    public int VendorUid { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;

    public decimal TotalOutstanding { get; set; }
    public decimal TotalPaid { get; set; }

    [Display(Name = "Purchase Invoice")]
    public int? PurchaseInvoiceUid { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    [Display(Name = "Pay amount")]
    public decimal Amount { get; set; }

    [Display(Name = "Payment Method")]
    public int? PaymentMethodUid { get; set; }

    [Display(Name = "Payment Date")]
    public DateTime PaymentDate { get; set; } = DateTime.Now;

    [StringLength(100), Display(Name = "Reference No")]
    public string? ReferenceNo { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    public IEnumerable<SelectListItem> InvoiceOptions { get; set; } = [];
    public IEnumerable<SelectListItem> PaymentMethodOptions { get; set; } = [];
    public List<VendorOpenInvoiceRowViewModel> OpenInvoices { get; set; } = [];
    public List<VendorPaymentListItemViewModel> Payments { get; set; } = [];
}

public sealed class VendorOpenInvoiceRowViewModel
{
    public int Uid { get; set; }
    public string PurchaseInvoiceNo { get; set; } = string.Empty;
    public DateTime PurchaseDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
}
