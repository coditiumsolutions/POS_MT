using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class VendorsController : Controller
{
    private const string VendorCodePrefix = "VEN-";
    private const int VendorCodeStartNumber = 1000;

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;

    public VendorsController(POSDbContext db, IAuditService audit, INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
    }

    private void EnsureVendorsNav() => _navContext.SetArea("Vendors");
    private void MarkVendorCodeReadonly() => ViewBag.VendorCodeReadonly = true;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureVendorsNav();
        return View(await _db.Vendors.AsNoTracking().OrderBy(x => x.VendorName).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureVendorsNav();
        MarkVendorCodeReadonly();
        var model = new VendorFormViewModel
        {
            IsActive = true,
            VendorCode = await GenerateNextVendorCodeAsync(ct)
        };
        await PopulateCityOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        MarkVendorCodeReadonly();

        // Always assign server-side so codes stay sequential and unique.
        model.VendorCode = await GenerateNextVendorCodeAsync(ct);
        ModelState.Remove(nameof(model.VendorCode));

        if (!ModelState.IsValid)
        {
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }

        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode, ct))
        {
            model.VendorCode = await GenerateNextVendorCodeAsync(ct);
            if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode, ct))
            {
                ModelState.AddModelError(nameof(model.VendorCode), "Could not generate a unique vendor code. Please try again.");
                await PopulateCityOptionsAsync(model, ct);
                return View(model);
            }
        }

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;
        _db.Vendors.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Vendors", e.Uid.ToString(), $"Created vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        var model = ToForm(e);
        await PopulateCityOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.VendorCode), "Vendor code already exists.");
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }
        Apply(e, model);
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Vendors", e.Uid.ToString(), $"Updated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpGet]
    public async Task<IActionResult> Pay(int id, int? purchaseInvoiceUid, CancellationToken ct)
    {
        EnsureVendorsNav();
        var vendor = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (vendor is null) return NotFound();

        var model = await BuildVendorPayViewModelAsync(vendor, purchaseInvoiceUid, amount: null, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(VendorPayViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        var vendor = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == model.VendorUid, ct);
        if (vendor is null) return NotFound();

        if (model.Amount <= 0)
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than zero.");
        if (model.PurchaseInvoiceUid is null or <= 0)
            ModelState.AddModelError(nameof(model.PurchaseInvoiceUid), "Select a purchase invoice to pay.");

        if (!ModelState.IsValid)
        {
            var invalid = await BuildVendorPayViewModelAsync(vendor, model.PurchaseInvoiceUid, model.Amount, ct);
            invalid.PaymentMethodUid = model.PaymentMethodUid;
            invalid.PaymentDate = model.PaymentDate;
            invalid.ReferenceNo = model.ReferenceNo;
            invalid.Remarks = model.Remarks;
            return View(invalid);
        }

        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(
            x => x.Uid == model.PurchaseInvoiceUid!.Value && x.VendorUid == vendor.Uid, ct);
        if (invoice is null)
        {
            TempData["Error"] = "Purchase invoice was not found for this vendor.";
            return RedirectToAction(nameof(Pay), new { id = vendor.Uid });
        }

        if (string.Equals(invoice.InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Cancelled invoices cannot accept payments.";
            return RedirectToAction(nameof(Pay), new { id = vendor.Uid });
        }

        RecalculateInvoiceBalances(invoice);
        if (invoice.BalanceAmount <= 0)
        {
            TempData["Error"] = "This invoice has no remaining balance.";
            return RedirectToAction(nameof(Pay), new { id = vendor.Uid });
        }

        var amount = model.Amount;
        if (amount > invoice.BalanceAmount)
            amount = invoice.BalanceAmount;

        var payDate = model.PaymentDate == default ? DateTime.Now : model.PaymentDate;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = new VendorPayment
            {
                PaymentNo = $"VP-{DateTime.Now:yyyyMMddHHmmss}",
                PaymentDate = payDate,
                VendorUid = vendor.Uid,
                PurchaseInvoiceUid = invoice.Uid,
                PaymentMethodUid = model.PaymentMethodUid is > 0 ? model.PaymentMethodUid : null,
                Amount = amount,
                ReferenceNo = string.IsNullOrWhiteSpace(model.ReferenceNo) ? null : model.ReferenceNo.Trim(),
                Remarks = string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim(),
                CreatedDate = DateTime.Now
            };
            _db.VendorPayments.Add(payment);

            invoice.PaidAmount = Math.Min(invoice.NetAmount, invoice.PaidAmount + amount);
            invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
            invoice.PaymentStatus = invoice.PaidAmount <= 0
                ? "Unpaid"
                : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
            invoice.UpdatedDate = DateTime.Now;

            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Payment", "VendorPayments", payment.Uid.ToString(),
                $"Vendor payment {payment.PaymentNo} for {vendor.VendorCode} / {invoice.PurchaseInvoiceNo}: {amount:N2}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = "Payment failed: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(Pay), new { id = vendor.Uid });
        }

        TempData["Success"] = $"Paid {amount:N2} to {vendor.VendorName}. Remaining on {invoice.PurchaseInvoiceNo}: {invoice.BalanceAmount:N2}.";
        return RedirectToAction(nameof(Pay), new { id = vendor.Uid });
    }

    private async Task<VendorPayViewModel> BuildVendorPayViewModelAsync(
        Vendor vendor,
        int? selectedInvoiceUid,
        decimal? amount,
        CancellationToken ct)
    {
        // Repair stale Net/Balance from line totals so unpaid invoices appear in the dropdown.
        await SyncVendorPurchaseTotalsAsync(vendor.Uid, ct);

        var invoices = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.VendorUid == vendor.Uid && x.InvoiceStatus != "Cancelled")
            .OrderByDescending(x => x.PurchaseDate)
            .ThenByDescending(x => x.Uid)
            .Select(x => new VendorOpenInvoiceRowViewModel
            {
                Uid = x.Uid,
                PurchaseInvoiceNo = x.PurchaseInvoiceNo,
                PurchaseDate = x.PurchaseDate,
                NetAmount = x.NetAmount,
                PaidAmount = x.PaidAmount,
                BalanceAmount = x.BalanceAmount,
                PaymentStatus = x.PaymentStatus
            })
            .ToListAsync(ct);

        foreach (var inv in invoices)
        {
            var computedBalance = Math.Max(0, inv.NetAmount - inv.PaidAmount);
            if (inv.BalanceAmount != computedBalance)
                inv.BalanceAmount = computedBalance;
            if (string.IsNullOrWhiteSpace(inv.PaymentStatus) || inv.PaymentStatus == "Unpaid")
            {
                inv.PaymentStatus = inv.PaidAmount <= 0
                    ? "Unpaid"
                    : inv.PaidAmount >= inv.NetAmount && inv.NetAmount > 0 ? "Paid" : "Partial";
            }
        }

        // Prefer invoices still owed; if none, still show unpaid/partial (even when stored totals were 0).
        var openInvoices = invoices
            .Where(x => x.BalanceAmount > 0
                        || !string.Equals(x.PaymentStatus, "Paid", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (openInvoices.Count == 0)
            openInvoices = invoices;

        var payments = await (
            from p in _db.VendorPayments.AsNoTracking()
            where p.VendorUid == vendor.Uid
            join i in _db.PurchaseInvoices.AsNoTracking() on p.PurchaseInvoiceUid equals i.Uid into ij
            from i in ij.DefaultIfEmpty()
            orderby p.PaymentDate descending, p.Uid descending
            select new VendorPaymentListItemViewModel
            {
                Uid = p.Uid,
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                PurchaseInvoiceNo = i != null ? i.PurchaseInvoiceNo : (p.PurchaseInvoiceUid.HasValue ? "#" + p.PurchaseInvoiceUid : "—"),
                Amount = p.Amount,
                ReferenceNo = p.ReferenceNo
            }).Take(50).ToListAsync(ct);

        var totalPaid = await _db.VendorPayments.AsNoTracking()
            .Where(x => x.VendorUid == vendor.Uid)
            .SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;

        var selected = selectedInvoiceUid is > 0
            ? openInvoices.FirstOrDefault(x => x.Uid == selectedInvoiceUid.Value)
            : openInvoices.FirstOrDefault(x => x.BalanceAmount > 0) ?? openInvoices.FirstOrDefault();

        var payAmount = amount is > 0
            ? amount.Value
            : selected?.BalanceAmount ?? 0m;

        return new VendorPayViewModel
        {
            VendorUid = vendor.Uid,
            VendorCode = vendor.VendorCode,
            VendorName = vendor.VendorName,
            TotalOutstanding = openInvoices.Sum(x => Math.Max(0, x.BalanceAmount)),
            TotalPaid = totalPaid,
            PurchaseInvoiceUid = selected?.Uid,
            Amount = payAmount,
            PaymentDate = DateTime.Now,
            OpenInvoices = openInvoices,
            Payments = payments,
            InvoiceOptions = openInvoices.Select(x => new SelectListItem
            {
                Value = x.Uid.ToString(),
                Text = $"{x.PurchaseInvoiceNo} — Bal {x.BalanceAmount:N2} ({x.PaymentStatus})",
                Selected = selected != null && x.Uid == selected.Uid
            }).ToList(),
            PaymentMethodOptions = await BuildDistinctPaymentMethodOptionsAsync(ct)
        };
    }

    private async Task SyncVendorPurchaseTotalsAsync(int vendorUid, CancellationToken ct)
    {
        var invoices = await _db.PurchaseInvoices
            .Where(x => x.VendorUid == vendorUid && x.InvoiceStatus != "Cancelled")
            .ToListAsync(ct);
        if (invoices.Count == 0) return;

        var ids = invoices.Select(x => x.Uid).ToList();
        var lineTotals = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => ids.Contains(x.PurchaseInvoiceUid))
            .GroupBy(x => x.PurchaseInvoiceUid)
            .Select(g => new { PurchaseInvoiceUid = g.Key, SubTotal = g.Sum(x => x.LineTotal) })
            .ToDictionaryAsync(x => x.PurchaseInvoiceUid, x => x.SubTotal, ct);

        var changed = false;
        foreach (var invoice in invoices)
        {
            var beforeSub = invoice.SubTotal;
            var beforeNet = invoice.NetAmount;
            var beforePaid = invoice.PaidAmount;
            var beforeBal = invoice.BalanceAmount;
            var beforeStatus = invoice.PaymentStatus;

            if (lineTotals.TryGetValue(invoice.Uid, out var subTotal) && subTotal > 0)
            {
                invoice.SubTotal = subTotal;
                invoice.NetAmount = Math.Max(0, subTotal + invoice.TaxAmount + invoice.OtherCharges - invoice.DiscountAmount);
            }

            RecalculateInvoiceBalances(invoice);

            if (invoice.SubTotal != beforeSub
                || invoice.NetAmount != beforeNet
                || invoice.PaidAmount != beforePaid
                || invoice.BalanceAmount != beforeBal
                || !string.Equals(invoice.PaymentStatus, beforeStatus, StringComparison.Ordinal))
            {
                invoice.UpdatedDate = DateTime.Now;
                changed = true;
            }
        }

        if (changed)
            await _db.SaveChangesAsync(ct);
    }

    private async Task<List<SelectListItem>> BuildDistinctPaymentMethodOptionsAsync(CancellationToken ct)
    {
        var methods = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.PaymentName)
            .ThenBy(x => x.Uid)
            .Select(x => new { x.Uid, x.PaymentName })
            .ToListAsync(ct);

        return methods
            .GroupBy(x => x.PaymentName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.PaymentName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SelectListItem { Value = x.Uid.ToString(), Text = x.PaymentName })
            .ToList();
    }

    private static void RecalculateInvoiceBalances(PurchaseInvoice invoice)
    {
        if (invoice.PaidAmount < 0) invoice.PaidAmount = 0;
        if (invoice.NetAmount < 0) invoice.NetAmount = 0;
        if (invoice.PaidAmount > invoice.NetAmount) invoice.PaidAmount = invoice.NetAmount;
        invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        invoice.PaymentStatus = invoice.PaidAmount <= 0
            ? "Unpaid"
            : invoice.PaidAmount >= invoice.NetAmount && invoice.NetAmount > 0 ? "Paid" : "Partial";
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.IsActive = false;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "Vendors", e.Uid.ToString(), $"Deactivated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null)
        {
            TempData["Error"] = "Vendor was not found.";
            return RedirectToAction(nameof(Index));
        }

        var code = e.VendorCode;
        var purchaseCount = await _db.PurchaseInvoices.CountAsync(x => x.VendorUid == id, ct);
        if (purchaseCount > 0)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because {purchaseCount} purchase invoice(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        var paymentCount = await _db.VendorPayments.CountAsync(x => x.VendorUid == id, ct);
        if (paymentCount > 0)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because {paymentCount} vendor payment(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.Vendors.Remove(e);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "Vendors", id.ToString(), $"Deleted vendor {code}", ct);
            TempData["Success"] = $"Vendor {code} deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because other records still use it.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateNextVendorCodeAsync(CancellationToken ct)
    {
        var existing = await _db.Vendors.AsNoTracking()
            .Where(x => x.VendorCode.StartsWith(VendorCodePrefix))
            .Select(x => x.VendorCode)
            .ToListAsync(ct);

        var maxNumber = VendorCodeStartNumber - 1;
        foreach (var code in existing)
        {
            var suffix = code.Length > VendorCodePrefix.Length
                ? code[VendorCodePrefix.Length..]
                : string.Empty;
            if (int.TryParse(suffix, out var number) && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"{VendorCodePrefix}{maxNumber + 1}";
    }

    private static VendorFormViewModel ToForm(Vendor e) => new()
    {
        Uid = e.Uid,
        VendorCode = e.VendorCode,
        VendorName = e.VendorName,
        ContactPerson = e.ContactPerson,
        MobileNo = e.MobileNo,
        PhoneNo = e.PhoneNo,
        Email = e.Email,
        Address = e.Address,
        City = e.City,
        Ntn = e.Ntn,
        Strn = e.Strn,
        OpeningBalance = e.OpeningBalance,
        CreditLimit = e.CreditLimit,
        PaymentTerms = e.PaymentTerms,
        IsActive = e.IsActive
    };

    private static Vendor ToEntity(VendorFormViewModel m) => new()
    {
        VendorCode = m.VendorCode.Trim(),
        VendorName = m.VendorName.Trim(),
        ContactPerson = m.ContactPerson,
        MobileNo = m.MobileNo,
        PhoneNo = m.PhoneNo,
        Email = m.Email,
        Address = m.Address,
        City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim(),
        Ntn = m.Ntn,
        Strn = m.Strn,
        OpeningBalance = m.OpeningBalance,
        CreditLimit = m.CreditLimit,
        PaymentTerms = m.PaymentTerms,
        IsActive = m.IsActive
    };

    private static void Apply(Vendor e, VendorFormViewModel m)
    {
        e.VendorCode = m.VendorCode.Trim();
        e.VendorName = m.VendorName.Trim();
        e.ContactPerson = m.ContactPerson;
        e.MobileNo = m.MobileNo;
        e.PhoneNo = m.PhoneNo;
        e.Email = m.Email;
        e.Address = m.Address;
        e.City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim();
        e.Ntn = m.Ntn;
        e.Strn = m.Strn;
        e.OpeningBalance = m.OpeningBalance;
        e.CreditLimit = m.CreditLimit;
        e.PaymentTerms = m.PaymentTerms;
        e.IsActive = m.IsActive;
    }

    private async Task PopulateCityOptionsAsync(VendorFormViewModel model, CancellationToken ct)
    {
        var citiesRaw = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == "Cities")
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        model.CityOptions = BuildCityOptions(citiesRaw, model.City);
    }

    private static List<SelectListItem> BuildCityOptions(string? csv, string? selected)
    {
        var values = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(selected)
            && !values.Any(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase)))
        {
            values.Insert(0, selected.Trim());
        }

        var items = new List<SelectListItem>
        {
            new() { Value = "", Text = "Select city…", Selected = string.IsNullOrWhiteSpace(selected) }
        };

        items.AddRange(values.Select(v => new SelectListItem
        {
            Value = v,
            Text = v,
            Selected = !string.IsNullOrWhiteSpace(selected)
                       && string.Equals(v, selected, StringComparison.OrdinalIgnoreCase)
        }));

        return items;
    }
}
