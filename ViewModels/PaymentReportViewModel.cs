namespace POS_MT.ViewModels;

public sealed class PaymentReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public int PaymentCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalAppliedToInvoice { get; set; }
    public decimal TotalAdvance { get; set; }
    public List<PaymentReportRowViewModel> Rows { get; set; } = [];
    public DateTime PrintedAt { get; set; } = DateTime.Now;
}

public sealed class PaymentReportRowViewModel
{
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string PaymentType { get; set; } = string.Empty;
    public string? InvoiceNo { get; set; }
    public string? PaymentMethod { get; set; }
    public string? ReferenceNo { get; set; }
    public decimal Amount { get; set; }
    public decimal AppliedToInvoiceAmount { get; set; }
    public decimal AdvanceAmount => Math.Max(0, Amount - AppliedToInvoiceAmount);
    public string? Remarks { get; set; }
}
