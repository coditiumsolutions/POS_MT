using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "CanViewSales")]
public class SalesInvoiceDetailsController : Controller
{
    private const string MonthlySupplyRemarkPrefix = "Monthly Supply";
    private const string MonthlyEntryDatePrefix = "EntryDate:";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;

    public SalesInvoiceDetailsController(POSDbContext db, IAuditService audit, INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
    }

    private void EnsureSalesInvoiceNav() => _navContext.SetArea("SalesInvoice");

    private static List<string> BuildEntryDateRemarkKeys(DateTime startDate, DateTime toInclusive)
    {
        var keys = new List<string>();
        for (var day = startDate.Date; day <= toInclusive.Date; day = day.AddDays(1))
            keys.Add($"{MonthlyEntryDatePrefix}{day:yyyy-MM-dd}");
        return keys;
    }

    private static DateTime ResolveLineDate(string? invoiceRemarks, string? detailRemarks, DateTime invoiceDate)
    {
        if (!string.IsNullOrWhiteSpace(invoiceRemarks)
            && invoiceRemarks.StartsWith(MonthlySupplyRemarkPrefix, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(detailRemarks)
            && detailRemarks.StartsWith(MonthlyEntryDatePrefix, StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParse(detailRemarks[MonthlyEntryDatePrefix.Length..], CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var entryDate))
        {
            return entryDate.Date;
        }

        return invoiceDate;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? customerType, int? month, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureSalesInvoiceNav();
        ViewData["Title"] = "Invoice Details";
        var fromDay = dateFrom?.Date;
        var toInclusive = dateTo?.Date;
        if (fromDay.HasValue && toInclusive.HasValue && toInclusive < fromDay)
            (fromDay, toInclusive) = (toInclusive, fromDay);
        var toExclusive = toInclusive?.AddDays(1);
        var hasDateFilter = fromDay.HasValue || toInclusive.HasValue;
        List<string> entryDateKeys = [];
        DateTime? monthSpanStart = null;
        DateTime? monthSpanEndExclusive = null;
        if (hasDateFilter)
        {
            var entryStart = fromDay ?? toInclusive!.Value;
            var entryEnd = toInclusive ?? fromDay!.Value;
            entryDateKeys = BuildEntryDateRemarkKeys(entryStart, entryEnd);
            monthSpanStart = new DateTime(entryStart.Year, entryStart.Month, 1);
            monthSpanEndExclusive = new DateTime(entryEnd.Year, entryEnd.Month, 1).AddMonths(1);
        }

        var query =
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            select new { d, inv, c };

        if (string.Equals(customerType, "walkin", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (string.Equals(customerType, "credit", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                (x.inv.Remarks == null || !x.inv.Remarks.StartsWith(MonthlySupplyRemarkPrefix)));
        }
        else if (string.Equals(customerType, "monthly", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                x.inv.Remarks != null &&
                x.inv.Remarks.StartsWith(MonthlySupplyRemarkPrefix));
        }

        if (hasDateFilter)
        {
            var keys = entryDateKeys;
            var spanStart = monthSpanStart!.Value;
            var spanEnd = monthSpanEndExclusive!.Value;
            query = query.Where(x =>
                ((x.inv.Remarks == null || !x.inv.Remarks.StartsWith(MonthlySupplyRemarkPrefix))
                 && (!fromDay.HasValue || x.inv.InvoiceDate >= fromDay.Value)
                 && (!toExclusive.HasValue || x.inv.InvoiceDate < toExclusive.Value))
                ||
                (x.inv.Remarks != null
                 && x.inv.Remarks.StartsWith(MonthlySupplyRemarkPrefix)
                 && x.inv.InvoiceDate >= spanStart
                 && x.inv.InvoiceDate < spanEnd
                 && x.d.Remarks != null
                 && keys.Contains(x.d.Remarks)));
        }
        else if (month is >= 1 and <= 12)
        {
            query = query.Where(x => x.inv.InvoiceDate.Month == month.Value);
        }

        var raw = await query
            .OrderByDescending(x => x.inv.InvoiceDate)
            .ThenByDescending(x => x.inv.Uid)
            .ThenBy(x => x.d.Uid)
            .Select(x => new
            {
                DetailUid = x.d.Uid,
                SalesInvoiceUid = x.inv.Uid,
                InvoiceNo = x.inv.InvoiceNo,
                x.inv.InvoiceDate,
                InvoiceRemarks = x.inv.Remarks,
                DetailRemarks = x.d.Remarks,
                CustomerName = x.c != null ? x.c.CustomerName : "—",
                CustomerCode = x.c != null ? x.c.CustomerCode : null,
                CustomerNameRaw = x.c != null ? x.c.CustomerName : null,
                ProductCode = x.d.ProductCode ?? string.Empty,
                ProductName = x.d.ProductName ?? string.Empty,
                Quantity = x.d.Quantity,
                UnitPrice = x.d.UnitPrice,
                LineTotal = x.d.LineTotal,
                InvoiceStatus = x.inv.InvoiceStatus
            })
            .ToListAsync(ct);

        var rows = raw.Select(x =>
        {
            var isWalkIn = x.CustomerCode == "C001"
                || (!string.IsNullOrWhiteSpace(x.CustomerNameRaw)
                    && (x.CustomerNameRaw.Contains("Walk-in", StringComparison.OrdinalIgnoreCase)
                        || x.CustomerNameRaw.Contains("Walk In", StringComparison.OrdinalIgnoreCase)));
            var isMonthly = !string.IsNullOrWhiteSpace(x.InvoiceRemarks)
                && x.InvoiceRemarks.StartsWith(MonthlySupplyRemarkPrefix, StringComparison.Ordinal);
            return new SalesInvoiceDetailListItemViewModel
            {
                DetailUid = x.DetailUid,
                SalesInvoiceUid = x.SalesInvoiceUid,
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = ResolveLineDate(x.InvoiceRemarks, x.DetailRemarks, x.InvoiceDate),
                CustomerName = x.CustomerName,
                CustomerType = isWalkIn ? "Walk-in" : (isMonthly ? "Monthly" : "Credit"),
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                LineTotal = x.LineTotal,
                InvoiceStatus = x.InvoiceStatus
            };
        })
        .OrderByDescending(x => x.InvoiceDate)
        .ThenByDescending(x => x.SalesInvoiceUid)
        .ThenBy(x => x.DetailUid)
        .ToList();

        return View(new SalesInvoiceDetailIndexViewModel
        {
            CustomerType = customerType,
            Month = month is >= 1 and <= 12 ? month : null,
            DateFrom = fromDay,
            DateTo = toInclusive,
            Rows = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureSalesInvoiceNav();
        ViewData["Title"] = "Invoice Detail";

        var row = await (
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            join p in _db.Products.AsNoTracking() on d.ProductUid equals p.Uid into pj
            from p in pj.DefaultIfEmpty()
            where d.Uid == id
            select new SalesInvoiceDetailViewModel
            {
                DetailUid = d.Uid,
                SalesInvoiceUid = inv.Uid,
                InvoiceNo = inv.InvoiceNo,
                InvoiceDate = inv.InvoiceDate,
                InvoiceStatus = inv.InvoiceStatus,
                PaymentStatus = inv.PaymentStatus,
                InvoiceNetAmount = inv.NetAmount,
                CustomerCode = c != null ? c.CustomerCode : "—",
                CustomerName = c != null ? c.CustomerName : "—",
                CustomerType = c == null
                    ? "—"
                    : (c.CustomerCode == "C001" ||
                       c.CustomerName.Contains("Walk-in") ||
                       c.CustomerName.Contains("Walk-In") ||
                       c.CustomerName.Contains("Walk In")
                        ? "Walk-in"
                        : (inv.Remarks != null && inv.Remarks.StartsWith("Monthly Supply")
                            ? "Monthly"
                            : "Credit")),
                ProductUid = d.ProductUid,
                ProductCode = d.ProductCode ?? (p != null ? p.ProductCode : string.Empty),
                ProductName = d.ProductName ?? (p != null ? p.ProductName : string.Empty),
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountPercent = d.DiscountPercent,
                DiscountAmount = d.DiscountAmount,
                TaxPercent = d.TaxPercent,
                TaxAmount = d.TaxAmount,
                LineTotal = d.LineTotal,
                CostPrice = d.CostPrice,
                DetailRemarks = d.Remarks,
                InvoiceRemarks = inv.Remarks
            }).FirstOrDefaultAsync(ct);

        return row is null ? NotFound() : View(row);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? customerType, int? month, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureSalesInvoiceNav();
        var detail = await _db.SalesInvoiceDetails.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (detail is null)
        {
            TempData["Error"] = "Invoice detail was not found.";
            return RedirectToAction(nameof(Index), new
            {
                customerType,
                month,
                dateFrom = dateFrom?.ToString("yyyy-MM-dd"),
                dateTo = dateTo?.ToString("yyyy-MM-dd")
            });
        }

        var invoiceUid = detail.SalesInvoiceUid;
        var productLabel = detail.ProductName ?? detail.ProductCode ?? detail.Uid.ToString();

        _db.SalesInvoiceDetails.Remove(detail);
        await _db.SaveChangesAsync(ct);

        var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Uid == invoiceUid, ct);
        if (invoice is not null)
        {
            var remainingTotal = await _db.SalesInvoiceDetails
                .Where(x => x.SalesInvoiceUid == invoiceUid)
                .SumAsync(x => (decimal?)x.LineTotal, ct) ?? 0m;

            invoice.SubTotal = remainingTotal;
            invoice.NetAmount = remainingTotal;
            invoice.BalanceAmount = Math.Max(0, remainingTotal - invoice.PaidAmount);
            if (invoice.PaidAmount >= remainingTotal && remainingTotal > 0)
            {
                invoice.PaymentStatus = "Paid";
                invoice.BalanceAmount = 0;
            }
            else if (invoice.PaidAmount > 0 && remainingTotal > 0)
            {
                invoice.PaymentStatus = "Partial";
            }
            else if (remainingTotal == 0)
            {
                invoice.PaymentStatus = "Unpaid";
                invoice.PaidAmount = 0;
                invoice.BalanceAmount = 0;
            }
            else
            {
                invoice.PaymentStatus = "Unpaid";
            }

            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync("Delete", "SalesInvoiceDetails", id.ToString(),
            $"Deleted invoice detail '{productLabel}' from invoice UID {invoiceUid}.", ct);

        TempData["Success"] = "Invoice detail deleted.";
        return RedirectToAction(nameof(Index), new
        {
            customerType,
            month,
            dateFrom = dateFrom?.ToString("yyyy-MM-dd"),
            dateTo = dateTo?.ToString("yyyy-MM-dd")
        });
    }
}
