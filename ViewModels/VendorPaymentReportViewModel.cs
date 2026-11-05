namespace POS_MT.ViewModels;

public sealed class VendorPaymentReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<VendorPaymentReportRowViewModel> Rows { get; set; } = [];
    public DateTime PrintedAt { get; set; } = DateTime.Now;
}

public sealed class VendorPaymentReportRowViewModel
{
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public string? PurchaseInvoiceNo { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}
