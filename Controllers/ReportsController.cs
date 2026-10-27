using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.ViewModels;
using System.Globalization;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
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
    public async Task<IActionResult> TotalSale(int? month, int? year, string? saleType, DateTime? date, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Total Sale for Monthly";

        var now = DateTime.Now;
        var selectedDate = date?.Date;
        var selectedMonth = selectedDate?.Month
            ?? (month is >= 1 and <= 12 ? month.Value : now.Month);
        var allowedYears = new[] { 2025, 2026 };
        var selectedYear = selectedDate?.Year
            ?? (year is int y && allowedYears.Contains(y) ? y
                : (allowedYears.Contains(now.Year) ? now.Year : 2026));
        var selectedSaleType = NormalizeSaleType(saleType);

        DateTime start;
        DateTime end;
        if (selectedDate.HasValue)
        {
            start = selectedDate.Value;
            end = start.AddDays(1);
        }
        else
        {
            start = new DateTime(selectedYear, selectedMonth, 1);
            end = start.AddMonths(1);
        }

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
                SaleType = x.inv.InvoiceStatus == SalesReturnsController.ReturnStatus
                    ? "Return"
                    : (x.c == null
                        ? "—"
                        : (x.c.CustomerCode == "C001" ||
                           x.c.CustomerName.Contains("Walk-in") ||
                           x.c.CustomerName.Contains("Walk-In") ||
                           x.c.CustomerName.Contains("Walk In")
                            ? "Walk-in"
                            : "Monthly")),
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
            Date = selectedDate,
            SaleType = selectedSaleType,
            Months = BuildMonthOptions(selectedMonth),
            Years = BuildYearOptions(selectedYear),
            SaleTypes = BuildSaleTypeOptions(selectedSaleType),
            InvoiceCount = rows.Count,
            TotalNetAmount = rows.Sum(x => x.NetAmount),
            TotalPaidAmount = rows.Sum(x => x.PaidAmount),
            TotalBalanceAmount = rows.Sum(x => x.NetAmount - x.PaidAmount),
            Rows = rows
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> DailyReport(DateTime? date, string? saleType, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Daily Report";

        var selectedDate = (date ?? DateTime.Today).Date;
        var selectedSaleType = string.IsNullOrWhiteSpace(saleType)
            ? "All"
            : NormalizeSaleType(saleType);
        var dayStart = selectedDate;
        var dayEnd = dayStart.AddDays(1);

        var query =
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            where inv.InvoiceDate >= dayStart
                  && inv.InvoiceDate < dayEnd
                  && inv.InvoiceStatus != "Cancelled"
            select new { d, inv, c };

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

        var raw = await query
            .OrderBy(x => x.inv.InvoiceDate)
            .ThenBy(x => x.inv.Uid)
            .ThenBy(x => x.d.Uid)
            .Select(x => new
            {
                x.inv.Uid,
                x.inv.InvoiceDate,
                Item = x.d.ProductName ?? x.d.ProductCode ?? "—",
                x.d.Quantity,
                Amount = x.d.LineTotal,
                DeliveryCharges = x.inv.OtherCharges
            })
            .ToListAsync(ct);

        var seenInvoices = new HashSet<int>();
        var rows = raw.Select(x =>
        {
            var showDelivery = seenInvoices.Add(x.Uid);
            return new DailyReportRowViewModel
            {
                Date = x.InvoiceDate,
                Item = x.Item,
                Quantity = x.Quantity,
                Amount = x.Amount,
                DeliveryCharges = showDelivery ? x.DeliveryCharges : 0m
            };
        }).ToList();

        return View(new DailyReportViewModel
        {
            Date = selectedDate,
            SaleType = selectedSaleType,
            SaleTypes = BuildSaleTypeOptions(selectedSaleType),
            Rows = rows,
            TotalQuantity = rows.Sum(x => x.Quantity),
            TotalAmount = rows.Sum(x => x.Amount),
            TotalDeliveryCharges = rows.Sum(x => x.DeliveryCharges)
        });
    }

    private static IEnumerable<SelectListItem> BuildMonthOptions(int selectedMonth) =>
        Enumerable.Range(1, 12)
            .Select(m => new SelectListItem
            {
                Value = m.ToString(CultureInfo.InvariantCulture),
                Text = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m),
                Selected = m == selectedMonth
            });

    private static IEnumerable<SelectListItem> BuildYearOptions(int selectedYear) =>
    [
        new SelectListItem
        {
            Value = "2025",
            Text = "2025",
            Selected = selectedYear == 2025
        },
        new SelectListItem
        {
            Value = "2026",
            Text = "2026",
            Selected = selectedYear == 2026
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
