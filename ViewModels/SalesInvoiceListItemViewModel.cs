namespace POS_MT.ViewModels;

public sealed class SalesInvoiceListItemViewModel
{
    public int Uid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = "—";
    public string CustomerType { get; set; } = "—";
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}

public sealed class SalesInvoiceIndexViewModel
{
    public string? CustomerType { get; set; }
    public string? Search { get; set; }
    public List<SalesInvoiceListItemViewModel> Rows { get; set; } = [];
}

public sealed class SalesInvoiceDetailListItemViewModel
{
    public int DetailUid { get; set; }
    public int SalesInvoiceUid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = "—";
    public string CustomerType { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
}

public sealed class SalesInvoiceDetailIndexViewModel
{
    public string? CustomerType { get; set; }
    public string? Search { get; set; }
    public List<SalesInvoiceDetailListItemViewModel> Rows { get; set; } = [];
}

public sealed class SalesInvoiceDetailViewModel
{
    public int DetailUid { get; set; }
    public int SalesInvoiceUid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public decimal InvoiceNetAmount { get; set; }
    public string CustomerCode { get; set; } = "—";
    public string CustomerName { get; set; } = "—";
    public string CustomerType { get; set; } = "—";
    public int ProductUid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal CostPrice { get; set; }
    public string? DetailRemarks { get; set; }
    public string? InvoiceRemarks { get; set; }
}

public sealed class SalesInvoiceDetailsPageViewModel
{
    public int Uid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerCode { get; set; } = "—";
    public string CustomerName { get; set; } = "—";
    public string CustomerType { get; set; } = "—";
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal OtherCharges { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public List<SalesInvoiceDetailLineViewModel> Lines { get; set; } = [];
}

public sealed class SalesInvoiceDetailLineViewModel
{
    public int DetailUid { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineTotal { get; set; }
    public string? Remarks { get; set; }
}
