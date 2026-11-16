namespace POS_MT.ViewModels;

public sealed class PurchaseInvoicePrintViewModel
{
    public int Uid { get; set; }
    public string PurchaseInvoiceNo { get; set; } = string.Empty;
    public string? VendorInvoiceNo { get; set; }
    public DateTime PurchaseDate { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string? VendorMobile { get; set; }
    public string? BranchName { get; set; }
    public string? WarehouseName { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public List<PurchaseInvoicePrintLineViewModel> Lines { get; set; } = [];
    public List<PurchaseInvoicePrintPaymentViewModel> Payments { get; set; } = [];
}

public sealed class PurchaseInvoicePrintLineViewModel
{
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? BatchNo { get; set; }
}

public sealed class PurchaseInvoicePrintPaymentViewModel
{
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string? ReferenceNo { get; set; }
    public string? Remarks { get; set; }
}
