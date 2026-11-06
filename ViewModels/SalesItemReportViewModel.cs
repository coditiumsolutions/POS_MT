using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class SalesItemReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string SaleType { get; set; } = "All";
    public IEnumerable<SelectListItem> SaleTypes { get; set; } = [];
    public List<SalesItemReportRowViewModel> Rows { get; set; } = [];

    public int LineCount => Rows.Count;
    public decimal TotalQuantity => Rows.Sum(x => x.Quantity);
    public decimal TotalAmount => Rows.Sum(x => x.LineTotal);
}

public sealed class SalesItemReportRowViewModel
{
    public DateTime InvoiceDate { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public int SalesInvoiceUid { get; set; }
    public string CustomerName { get; set; } = "—";
    public string SaleType { get; set; } = "—";
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}
