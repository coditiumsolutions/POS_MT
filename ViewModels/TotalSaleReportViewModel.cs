using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public class TotalSaleReportViewModel
{
    public int Month { get; set; }
    public int Year { get; set; }
    public string SaleType { get; set; } = "Monthly";

    public IEnumerable<SelectListItem> Months { get; set; } = [];
    public IEnumerable<SelectListItem> Years { get; set; } = [];
    public IEnumerable<SelectListItem> SaleTypes { get; set; } = [];

    public int InvoiceCount { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public decimal TotalBalanceAmount { get; set; }

    public List<TotalSaleReportRowViewModel> Rows { get; set; } = [];
}

public class TotalSaleReportRowViewModel
{
    public int Uid { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public string CustomerName { get; set; } = "—";
    public string SaleType { get; set; } = "—";
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string InvoiceStatus { get; set; } = string.Empty;
    public string PaymentStatus { get; set; } = string.Empty;
}
