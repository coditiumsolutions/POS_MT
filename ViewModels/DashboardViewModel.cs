namespace POS_MT.ViewModels;

public sealed class DashboardViewModel
{
    public bool DatabaseConnected { get; set; }

    public string StatusMessage { get; set; } = string.Empty;

    public decimal? TodaysSales { get; set; }

    public decimal? TodaysPurchases { get; set; }

    public decimal? TodaysExpenses { get; set; }

    public decimal? CustomerReceivables { get; set; }

    public decimal? VendorPayables { get; set; }

    public decimal? CurrentStock { get; set; }

    public int? LowStockProducts { get; set; }
}
