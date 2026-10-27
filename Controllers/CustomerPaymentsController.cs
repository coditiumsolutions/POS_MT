using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "Staff")]
public class CustomerPaymentsController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICustomerAdvanceService _advance;
    private readonly INavContextService _navContext;

    public CustomerPaymentsController(
        POSDbContext db,
        IAuditService audit,
        ICustomerAdvanceService advance,
        INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _advance = advance;
        _navContext = navContext;
    }

    private void EnsureCustomersNav() => _navContext.SetArea("Customers");

    private async Task LoadLookups(int? customerUid, CancellationToken ct)
    {
        ViewBag.Customers = new SelectList(
            await _db.Customers.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.CustomerName).ToListAsync(ct),
            "Uid", "CustomerName", customerUid);

        var invoiceQuery = _db.SalesInvoices.AsNoTracking()
            .Where(x => x.InvoiceStatus != "Cancelled")
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Uid)
            .AsQueryable();

        if (customerUid is > 0)
            invoiceQuery = invoiceQuery.Where(x => x.CustomerUid == customerUid);

        var invoices = await invoiceQuery.Take(200)
            .Select(x => new { x.Uid, Label = x.InvoiceNo + " (" + x.BalanceAmount.ToString("N2") + " due)" })
            .ToListAsync(ct);
        ViewBag.Invoices = new SelectList(invoices, "Uid", "Label");

        ViewBag.PaymentMethods = new SelectList(
            await _db.PaymentMethods.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.PaymentName).ToListAsync(ct),
            "Uid", "PaymentName");
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);

        var rows = await (
            from p in _db.CustomerPayments.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on p.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            join inv in _db.SalesInvoices.AsNoTracking() on p.SalesInvoiceUid equals inv.Uid into ij
            from inv in ij.DefaultIfEmpty()
            orderby p.PaymentDate descending, p.Uid descending
            select new CustomerPaymentListItemViewModel
            {
                Uid = p.Uid,
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                CustomerName = c != null ? c.CustomerName : ("#" + p.CustomerUid),
                PaymentType = p.SalesInvoiceUid == null || p.AppliedToInvoiceAmount <= 0
                    ? "Advance"
                    : (p.Amount > p.AppliedToInvoiceAmount ? "Invoice + Advance" : "Against Invoice"),
                InvoiceNo = inv != null ? inv.InvoiceNo : null,
                Amount = p.Amount,
                AppliedToInvoiceAmount = p.AppliedToInvoiceAmount
            }).ToListAsync(ct);

        return View(rows);
    }

    public async Task<IActionResult> Create(int? customerUid, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        await LoadLookups(customerUid, ct);

        var model = new CustomerPaymentFormViewModel
        {
            PaymentDate = DateTime.Now,
            PaymentNo = $"CP-{DateTime.Now:yyyyMMddHHmmss}",
            PaymentType = "Advance",
            CustomerUid = customerUid ?? 0
        };
        if (customerUid is > 0)
            model.CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(customerUid.Value, ct);

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerPaymentFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        NormalizePaymentType(model);

        if (string.Equals(model.PaymentType, "AgainstInvoice", StringComparison.OrdinalIgnoreCase)
            && model.SalesInvoiceUid is null or <= 0)
        {
            ModelState.AddModelError(nameof(model.SalesInvoiceUid), "Select a sales invoice for this payment.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLookups(model.CustomerUid, ct);
            if (model.CustomerUid > 0)
                model.CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(model.CustomerUid, ct);
            return View(model);
        }

        if (string.Equals(model.PaymentType, "Advance", StringComparison.OrdinalIgnoreCase))
            model.SalesInvoiceUid = null;

        var e = new CustomerPayment
        {
            PaymentNo = model.PaymentNo.Trim(),
            PaymentDate = model.PaymentDate,
            CustomerUid = model.CustomerUid,
            SalesInvoiceUid = model.SalesInvoiceUid,
            PaymentMethodUid = model.PaymentMethodUid,
            Amount = model.Amount,
            ReferenceNo = model.ReferenceNo,
            Remarks = string.IsNullOrWhiteSpace(model.Remarks)
                ? (model.SalesInvoiceUid is null ? "Advance payment" : model.Remarks)
                : model.Remarks,
            CreatedDate = DateTime.Now
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.CustomerPayments.Add(e);
            await _advance.ApplyNewPaymentAsync(e, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Payment", "CustomerPayments", e.Uid.ToString(),
                $"Customer payment {e.PaymentNo}: {e.Amount:N2} (invoice {e.AppliedToInvoiceAmount:N2}, advance {e.Amount - e.AppliedToInvoiceAmount:N2})", ct);
            await tx.CommitAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadLookups(model.CustomerUid, ct);
            model.CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(model.CustomerUid, ct);
            return View(model);
        }

        var balance = await _advance.GetAdvanceBalanceAsync(model.CustomerUid, ct);
        TempData["Success"] = e.AppliedToInvoiceAmount > 0 && e.Amount > e.AppliedToInvoiceAmount
            ? $"Payment saved. Applied {e.AppliedToInvoiceAmount:N2} to invoice; advance credit now {balance:N2}."
            : e.AppliedToInvoiceAmount > 0
                ? $"Payment applied to invoice. Customer advance credit: {balance:N2}."
                : $"Advance payment saved. Customer advance credit: {balance:N2}.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        var e = await _db.CustomerPayments.FindAsync([id], ct);
        if (e is null) return NotFound();
        await LoadLookups(e.CustomerUid, ct);
        return View(new CustomerPaymentFormViewModel
        {
            Uid = e.Uid,
            PaymentNo = e.PaymentNo,
            PaymentDate = e.PaymentDate,
            CustomerUid = e.CustomerUid,
            PaymentType = e.SalesInvoiceUid is > 0 ? "AgainstInvoice" : "Advance",
            SalesInvoiceUid = e.SalesInvoiceUid,
            PaymentMethodUid = e.PaymentMethodUid,
            Amount = e.Amount,
            ReferenceNo = e.ReferenceNo,
            Remarks = e.Remarks,
            CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(e.CustomerUid, ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerPaymentFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        if (id != model.Uid) return BadRequest();
        NormalizePaymentType(model);

        if (string.Equals(model.PaymentType, "AgainstInvoice", StringComparison.OrdinalIgnoreCase)
            && model.SalesInvoiceUid is null or <= 0)
        {
            ModelState.AddModelError(nameof(model.SalesInvoiceUid), "Select a sales invoice for this payment.");
        }

        if (!ModelState.IsValid)
        {
            await LoadLookups(model.CustomerUid, ct);
            model.CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(model.CustomerUid, ct);
            return View(model);
        }

        if (string.Equals(model.PaymentType, "Advance", StringComparison.OrdinalIgnoreCase))
            model.SalesInvoiceUid = null;

        var e = await _db.CustomerPayments.FindAsync([id], ct);
        if (e is null) return NotFound();

        var prevCustomer = e.CustomerUid;
        var prevInvoice = e.SalesInvoiceUid;
        var prevAmount = e.Amount;
        var prevApplied = e.AppliedToInvoiceAmount;

        e.PaymentNo = model.PaymentNo.Trim();
        e.PaymentDate = model.PaymentDate;
        e.CustomerUid = model.CustomerUid;
        e.SalesInvoiceUid = model.SalesInvoiceUid;
        e.PaymentMethodUid = model.PaymentMethodUid;
        e.Amount = model.Amount;
        e.ReferenceNo = model.ReferenceNo;
        e.Remarks = model.Remarks;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _advance.ReplacePaymentAsync(e, prevCustomer, prevInvoice, prevAmount, prevApplied, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Update", "CustomerPayments", e.Uid.ToString(),
                $"Updated payment {e.PaymentNo}", ct);
            await tx.CommitAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadLookups(model.CustomerUid, ct);
            model.CurrentAdvanceBalance = await _advance.GetAdvanceBalanceAsync(model.CustomerUid, ct);
            return View(model);
        }

        TempData["Success"] = "Customer payment updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        var row = await (
            from p in _db.CustomerPayments.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on p.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            join inv in _db.SalesInvoices.AsNoTracking() on p.SalesInvoiceUid equals inv.Uid into ij
            from inv in ij.DefaultIfEmpty()
            where p.Uid == id
            select new
            {
                Payment = p,
                CustomerName = c != null ? c.CustomerName : null,
                AdvanceBalance = c != null ? c.AdvanceBalance : 0m,
                InvoiceNo = inv != null ? inv.InvoiceNo : null
            }).FirstOrDefaultAsync(ct);

        if (row is null) return NotFound();
        ViewBag.CustomerName = row.CustomerName;
        ViewBag.InvoiceNo = row.InvoiceNo;
        ViewBag.AdvanceBalance = row.AdvanceBalance;
        return View(row.Payment);
    }

    private static void NormalizePaymentType(CustomerPaymentFormViewModel model)
    {
        if (string.Equals(model.PaymentType, "AgainstInvoice", StringComparison.OrdinalIgnoreCase)
            || string.Equals(model.PaymentType, "Invoice", StringComparison.OrdinalIgnoreCase))
        {
            model.PaymentType = "AgainstInvoice";
        }
        else
        {
            model.PaymentType = "Advance";
        }
    }
}
