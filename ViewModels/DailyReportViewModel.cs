using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public class DailyReportViewModel
{
    public DateTime Date { get; set; }
    public string SaleType { get; set; } = "All";
    public IEnumerable<SelectListItem> SaleTypes { get; set; } = [];
    public List<DailyReportRowViewModel> Rows { get; set; } = [];
    public decimal TotalQuantity { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal TotalDeliveryCharges { get; set; }
}

public class DailyReportRowViewModel
{
    public DateTime Date { get; set; }
    public string Item { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal Amount { get; set; }
    public decimal DeliveryCharges { get; set; }
}
