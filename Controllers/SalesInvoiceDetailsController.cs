using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class SalesInvoiceDetailsController : Controller
{
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

    [HttpGet]
    public async Task<IActionResult> Index(string? customerType, int? month, CancellationToken ct)
    {
        EnsureSalesInvoiceNav();
        ViewData["Title"] = "Invoice Details";
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
                (x.inv.Remarks == null || !x.inv.Remarks.StartsWith("Monthly Supply")));
        }
        else if (string.Equals(customerType, "monthly", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                x.inv.Remarks != null &&
                x.inv.Remarks.StartsWith("Monthly Supply"));
        }

        if (month is >= 1 and <= 12)
        {
            query = query.Where(x => x.inv.InvoiceDate.Month == month.Value);
        }

        var rows = await query
            .OrderByDescending(x => x.inv.InvoiceDate)
            .ThenByDescending(x => x.inv.Uid)
            .ThenBy(x => x.d.Uid)
            .Select(x => new SalesInvoiceDetailListItemViewModel
            {
                DetailUid = x.d.Uid,
                SalesInvoiceUid = x.inv.Uid,
                InvoiceNo = x.inv.InvoiceNo,
                InvoiceDate = x.inv.InvoiceDate,
                CustomerName = x.c != null ? x.c.CustomerName : "—",
                CustomerType = x.c == null
                    ? "—"
                    : (x.c.CustomerCode == "C001" ||
                       x.c.CustomerName.Contains("Walk-in") ||
                       x.c.CustomerName.Contains("Walk-In") ||
                       x.c.CustomerName.Contains("Walk In")
                        ? "Walk-in"
                        : (x.inv.Remarks != null && x.inv.Remarks.StartsWith("Monthly Supply")
                            ? "Monthly"
                            : "Credit")),
                ProductCode = x.d.ProductCode ?? string.Empty,
                ProductName = x.d.ProductName ?? string.Empty,
                Quantity = x.d.Quantity,
                UnitPrice = x.d.UnitPrice,
                LineTotal = x.d.LineTotal,
                InvoiceStatus = x.inv.InvoiceStatus
            })
            .ToListAsync(ct);

        return View(new SalesInvoiceDetailIndexViewModel
        {
            CustomerType = customerType,
            Month = month is >= 1 and <= 12 ? month : null,
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
    public async Task<IActionResult> Delete(int id, string? customerType, int? month, CancellationToken ct)
    {
        EnsureSalesInvoiceNav();
        var detail = await _db.SalesInvoiceDetails.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (detail is null)
        {
            TempData["Error"] = "Invoice detail was not found.";
            return RedirectToAction(nameof(Index), new { customerType, month });
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
        return RedirectToAction(nameof(Index), new { customerType, month });
    }
}
