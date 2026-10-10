using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.ViewModels;
using System.Globalization;

namespace POS_MT.Controllers;

[Authorize]
public class ReportsController : Controller
{
    private readonly POSDbContext _db;
    private readonly INavContextService _navContext;

    public ReportsController(POSDbContext db, INavContextService navContext)
    {
        _db = db;
        _navContext = navContext;
    }

    private void EnsureReportsNav() => _navContext.SetArea("Reports");

    [HttpGet]
    public async Task<IActionResult> TotalSale(int? month, int? year, string? saleType, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Total Sale for Monthly";

        var now = DateTime.Now;
        var selectedMonth = month is >= 1 and <= 12 ? month.Value : now.Month;
        var selectedYear = year is int y && (y == now.Year || y == now.Year - 1) ? y : now.Year;
        var selectedSaleType = NormalizeSaleType(saleType);

        var start = new DateTime(selectedYear, selectedMonth, 1);
        var end = start.AddMonths(1);

        var query =
            from inv in _db.SalesInvoices.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            where inv.InvoiceDate >= start
                  && inv.InvoiceDate < end
                  && inv.InvoiceStatus != "Cancelled"
            select new { inv, c };

        if (selectedSaleType is "Cash" or "Walk-in")
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (selectedSaleType == "Monthly")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In"));
        }

        var rows = await query
            .OrderByDescending(x => x.inv.InvoiceDate)
            .ThenByDescending(x => x.inv.Uid)
            .Select(x => new TotalSaleReportRowViewModel
            {
                Uid = x.inv.Uid,
                InvoiceNo = x.inv.InvoiceNo,
                InvoiceDate = x.inv.InvoiceDate,
                CustomerName = x.c != null ? x.c.CustomerName : "—",
                SaleType = x.c == null
                    ? "—"
                    : (x.c.CustomerCode == "C001" ||
                       x.c.CustomerName.Contains("Walk-in") ||
                       x.c.CustomerName.Contains("Walk-In") ||
                       x.c.CustomerName.Contains("Walk In")
                        ? "Walk-in"
                        : "Monthly"),
                NetAmount = x.inv.NetAmount,
                PaidAmount = x.inv.PaidAmount,
                InvoiceStatus = x.inv.InvoiceStatus,
                PaymentStatus = x.inv.PaymentStatus
            })
            .ToListAsync(ct);

        var model = new TotalSaleReportViewModel
        {
            Month = selectedMonth,
            Year = selectedYear,
            SaleType = selectedSaleType,
            Months = BuildMonthOptions(selectedMonth),
            Years = BuildYearOptions(now.Year, selectedYear),
            SaleTypes = BuildSaleTypeOptions(selectedSaleType),
            InvoiceCount = rows.Count,
            TotalNetAmount = rows.Sum(x => x.NetAmount),
            TotalPaidAmount = rows.Sum(x => x.PaidAmount),
            TotalBalanceAmount = rows.Sum(x => x.NetAmount - x.PaidAmount),
            Rows = rows
        };

        return View(model);
    }

    private static IEnumerable<SelectListItem> BuildMonthOptions(int selectedMonth) =>
        Enumerable.Range(1, 12)
            .Select(m => new SelectListItem
            {
                Value = m.ToString(CultureInfo.InvariantCulture),
                Text = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m),
                Selected = m == selectedMonth
            });

    private static IEnumerable<SelectListItem> BuildYearOptions(int currentYear, int selectedYear) =>
    [
        new SelectListItem
        {
            Value = currentYear.ToString(CultureInfo.InvariantCulture),
            Text = $"Current ({currentYear})",
            Selected = selectedYear == currentYear
        },
        new SelectListItem
        {
            Value = (currentYear - 1).ToString(CultureInfo.InvariantCulture),
            Text = $"Previous ({currentYear - 1})",
            Selected = selectedYear == currentYear - 1
        }
    ];

    private static IEnumerable<SelectListItem> BuildSaleTypeOptions(string selectedSaleType) =>
    [
        new SelectListItem { Value = "All", Text = "All", Selected = selectedSaleType == "All" },
        new SelectListItem { Value = "Monthly", Text = "Monthly", Selected = selectedSaleType == "Monthly" },
        new SelectListItem { Value = "Cash", Text = "Cash", Selected = selectedSaleType == "Cash" },
        new SelectListItem { Value = "Walk-in", Text = "Walk-in", Selected = selectedSaleType == "Walk-in" }
    ];

    private static string NormalizeSaleType(string? saleType)
    {
        if (string.Equals(saleType, "Cash", StringComparison.OrdinalIgnoreCase))
        {
            return "Cash";
        }

        if (string.Equals(saleType, "Walk-in", StringComparison.OrdinalIgnoreCase)
            || string.Equals(saleType, "Walkin", StringComparison.OrdinalIgnoreCase))
        {
            return "Walk-in";
        }

        if (string.Equals(saleType, "All", StringComparison.OrdinalIgnoreCase))
        {
            return "All";
        }

        return "Monthly";
    }
}
