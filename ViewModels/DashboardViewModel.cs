namespace POS_MT.ViewModels;

public sealed class DashboardViewModel
{
    public bool DatabaseConnected { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public int ChartYear { get; set; }

    public decimal? TodaysSales { get; set; }

    public decimal? TodaysPurchases { get; set; }

    public decimal? TodaysExpenses { get; set; }

    public decimal? CustomerReceivables { get; set; }

    public decimal? VendorPayables { get; set; }

    public decimal? CurrentStock { get; set; }

    public int? LowStockProducts { get; set; }

    public List<string> MonthLabels { get; set; } = [];

    public List<decimal> MonthlySalesAmounts { get; set; } = [];

    public int SelectedItemMonth { get; set; }

    public string SelectedItemMonthName { get; set; } = string.Empty;

    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> ItemMonthOptions { get; set; } = [];

    public List<string> ItemNameLabels { get; set; } = [];

    public List<decimal> ItemQuantities { get; set; } = [];

    public List<decimal> MonthlyCashWalkInAmounts { get; set; } = [];

    public List<decimal> MonthlyCreditAmounts { get; set; } = [];
}
