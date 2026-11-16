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
    public int? Month { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public List<SalesInvoiceListItemViewModel> Rows { get; set; } = [];

    public decimal TotalNet => Rows.Sum(x => x.NetAmount);
    public decimal TotalPaid => Rows.Sum(x => x.PaidAmount);
}

public sealed class SalesReportPrintViewModel
{
    public DateTime PrintedAt { get; set; }
    public string? CustomerType { get; set; }
    public int? Month { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public IReadOnlyList<SalesInvoiceListItemViewModel> Rows { get; set; } = [];

    public decimal TotalNet => Rows.Sum(x => x.NetAmount);
    public decimal TotalPaid => Rows.Sum(x => x.PaidAmount);
    public decimal TotalBalance => Rows.Sum(x => x.NetAmount - x.PaidAmount);
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
    public int? Month { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public List<SalesInvoiceDetailListItemViewModel> Rows { get; set; } = [];

    public decimal TotalLineTotal => Rows.Sum(x => x.LineTotal);
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
    /// <summary>Sum of other unpaid invoice balances for this customer (previous dues).</summary>
    public decimal PreviousBalanceAmount { get; set; }
    /// <summary>PreviousBalanceAmount + BalanceAmount (total still due including this bill).</summary>
    public decimal GrandTotalDue { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
    public string? Remarks { get; set; }
    public List<SalesInvoiceDetailLineViewModel> Lines { get; set; } = [];
    public List<SalesInvoicePaymentRowViewModel> Payments { get; set; } = [];
}

public sealed class SalesInvoicePaymentRowViewModel
{
    public int Uid { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public decimal AppliedToInvoiceAmount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class CustomerUnpaidInvoiceRowViewModel
{
    public int Uid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal BalanceAmount { get; set; }
    public string Label { get; set; } = "—";
}

public sealed class CustomerBillHistoryRowViewModel
{
    public int Uid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    /// <summary>Bill/supply day shown to the user (entry date for monthly lines, else invoice date).</summary>
    public DateTime InvoiceDate { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal BalanceAmount { get; set; }
    public string PaymentStatus { get; set; } = string.Empty;
    public string InvoiceStatus { get; set; } = string.Empty;
    public string Label { get; set; } = "—";
    public string ItemsSummary { get; set; } = string.Empty;
    /// <summary>True when this row is a day slice of a Monthly Supply invoice.</summary>
    public bool IsMonthlyDayBill { get; set; }
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
