using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.ViewModels;

namespace POS_MT.Services;

public sealed class DashboardService : IDashboardService
{
    private const int TopItemCount = 12;

    private readonly POSDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(POSDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardViewModel> GetSnapshotAsync(int? itemMonth = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.Now;
        var selectedItemMonth = itemMonth is >= 1 and <= 12 ? itemMonth.Value : now.Month;

        var model = new DashboardViewModel
        {
            ChartYear = now.Year,
            SelectedItemMonth = selectedItemMonth,
            SelectedItemMonthName = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(selectedItemMonth),
            ItemMonthOptions = Enumerable.Range(1, 12)
                .Select(m => new SelectListItem
                {
                    Value = m.ToString(CultureInfo.InvariantCulture),
                    Text = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m),
                    Selected = m == selectedItemMonth
                })
                .ToList(),
            MonthLabels = Enumerable.Range(1, 12)
                .Select(m => CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(m))
                .ToList(),
            MonthlySalesAmounts = Enumerable.Repeat(0m, 12).ToList(),
            MonthlyCashWalkInAmounts = Enumerable.Repeat(0m, 12).ToList(),
            MonthlyCreditAmounts = Enumerable.Repeat(0m, 12).ToList()
        };

        try
        {
            model.DatabaseConnected = await _db.Database.CanConnectAsync(cancellationToken);
            if (!model.DatabaseConnected)
            {
                model.StatusMessage = "The application could not open the POS_MT database. Check the server name and that the database exists.";
                return model;
            }

            model.StatusMessage = $"Sales overview for {model.ChartYear} — amount, items by name, and Cash / Walk-in vs Monthly.";

            var yearStart = new DateTime(model.ChartYear, 1, 1);
            var yearEnd = yearStart.AddYears(1);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // SQLite cannot Sum(decimal) in SQL — load values then aggregate in memory.
            model.TodaysSales = (await _db.SalesInvoices.AsNoTracking()
                .Where(x => x.InvoiceDate >= today && x.InvoiceDate < tomorrow && x.InvoiceStatus != "Cancelled")
                .Select(x => x.NetAmount)
                .ToListAsync(cancellationToken)).Sum();

            model.TodaysPurchases = (await _db.PurchaseInvoices.AsNoTracking()
                .Where(x => x.PurchaseDate >= today && x.PurchaseDate < tomorrow && x.InvoiceStatus != "Cancelled")
                .Select(x => x.NetAmount)
                .ToListAsync(cancellationToken)).Sum();

            model.TodaysExpenses = (await _db.Expenses.AsNoTracking()
                .Where(x => x.ExpenseDate >= today && x.ExpenseDate < tomorrow)
                .Select(x => x.Amount)
                .ToListAsync(cancellationToken)).Sum();

            model.CustomerReceivables = (await _db.SalesInvoices.AsNoTracking()
                .Where(x => x.InvoiceStatus != "Cancelled")
                .Select(x => x.BalanceAmount)
                .ToListAsync(cancellationToken)).Sum();

            model.VendorPayables = (await _db.PurchaseInvoices.AsNoTracking()
                .Where(x => x.InvoiceStatus != "Cancelled")
                .Select(x => x.BalanceAmount)
                .ToListAsync(cancellationToken)).Sum();

            model.CurrentStock = (await _db.Inventories.AsNoTracking()
                .Select(x => x.CurrentQuantity)
                .ToListAsync(cancellationToken)).Sum();

            model.LowStockProducts = await _db.Inventories.AsNoTracking()
                .CountAsync(x => x.CurrentQuantity <= x.MinimumStock, cancellationToken);

            var yearSales = await _db.SalesInvoices.AsNoTracking()
                .Where(x => x.InvoiceDate >= yearStart
                            && x.InvoiceDate < yearEnd
                            && x.InvoiceStatus != "Cancelled")
                .Select(x => new { x.InvoiceDate.Month, x.NetAmount })
                .ToListAsync(cancellationToken);

            foreach (var row in yearSales.GroupBy(x => x.Month).Select(g => new { Month = g.Key, Total = g.Sum(x => x.NetAmount) }))
            {
                if (row.Month is >= 1 and <= 12)
                {
                    model.MonthlySalesAmounts[row.Month - 1] = row.Total;
                }
            }

            var itemMonthStart = new DateTime(model.ChartYear, selectedItemMonth, 1);
            var itemMonthEnd = itemMonthStart.AddMonths(1);

            var itemDetailRows = await (
                from d in _db.SalesInvoiceDetails.AsNoTracking()
                join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
                where inv.InvoiceDate >= itemMonthStart
                      && inv.InvoiceDate < itemMonthEnd
                      && inv.InvoiceStatus != "Cancelled"
                select new { ItemName = d.ProductName ?? d.ProductCode ?? "Unknown Item", d.Quantity }
            ).ToListAsync(cancellationToken);

            var itemRows = itemDetailRows
                .GroupBy(x => x.ItemName)
                .Select(g => new { ItemName = g.Key, Quantity = g.Sum(x => x.Quantity) })
                .OrderByDescending(x => x.Quantity)
                .Take(TopItemCount)
                .ToList();

            model.ItemNameLabels = itemRows.Select(x => x.ItemName).ToList();
            model.ItemQuantities = itemRows.Select(x => x.Quantity).ToList();

            var cashWalkInRows = await (
                from inv in _db.SalesInvoices.AsNoTracking()
                join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid
                where inv.InvoiceDate >= yearStart
                      && inv.InvoiceDate < yearEnd
                      && inv.InvoiceStatus != "Cancelled"
                      && (c.CustomerCode == "C001"
                          || c.CustomerName.Contains("Walk-in")
                          || c.CustomerName.Contains("Walk-In")
                          || c.CustomerName.Contains("Walk In"))
                select new { inv.InvoiceDate.Month, inv.NetAmount }
            ).ToListAsync(cancellationToken);

            foreach (var row in cashWalkInRows.GroupBy(x => x.Month).Select(g => new { Month = g.Key, Total = g.Sum(x => x.NetAmount) }))
            {
                if (row.Month is >= 1 and <= 12)
                {
                    model.MonthlyCashWalkInAmounts[row.Month - 1] = row.Total;
                }
            }

            var monthlyRows = await (
                from inv in _db.SalesInvoices.AsNoTracking()
                join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid
                where inv.InvoiceDate >= yearStart
                      && inv.InvoiceDate < yearEnd
                      && inv.InvoiceStatus != "Cancelled"
                      && c.CustomerCode != "C001"
                      && !c.CustomerName.Contains("Walk-in")
                      && !c.CustomerName.Contains("Walk-In")
                      && !c.CustomerName.Contains("Walk In")
                select new { inv.InvoiceDate.Month, inv.NetAmount }
            ).ToListAsync(cancellationToken);

            foreach (var row in monthlyRows.GroupBy(x => x.Month).Select(g => new { Month = g.Key, Total = g.Sum(x => x.NetAmount) }))
            {
                if (row.Month is >= 1 and <= 12)
                {
                    model.MonthlyCreditAmounts[row.Month - 1] = row.Total;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard snapshot failed.");
            model.DatabaseConnected = false;
            model.StatusMessage = "The application could not load dashboard data. Check the database connection.";
        }

        return model;
    }
}
