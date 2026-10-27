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
public class VendorPaymentsController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public VendorPaymentsController(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private async Task LoadLookups(CancellationToken ct)
    {
        ViewBag.Vendors = new SelectList(
            await _db.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.VendorName).ToListAsync(ct),
            "Uid", "VendorName");
        ViewBag.Invoices = new SelectList(
            await _db.PurchaseInvoices.AsNoTracking().OrderByDescending(x => x.Uid).Take(200).ToListAsync(ct),
            "Uid", "PurchaseInvoiceNo");
        ViewBag.PaymentMethods = new SelectList(
            await _db.PaymentMethods.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.PaymentName).ToListAsync(ct),
            "Uid", "PaymentName");
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var rows = await (
            from p in _db.VendorPayments.AsNoTracking()
            join v in _db.Vendors.AsNoTracking() on p.VendorUid equals v.Uid into vj
            from v in vj.DefaultIfEmpty()
            join i in _db.PurchaseInvoices.AsNoTracking() on p.PurchaseInvoiceUid equals i.Uid into ij
            from i in ij.DefaultIfEmpty()
            orderby p.PaymentDate descending
            select new VendorPaymentListItemViewModel
            {
                Uid = p.Uid,
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                VendorName = v != null ? v.VendorName : ("#" + p.VendorUid),
                PurchaseInvoiceNo = i != null ? i.PurchaseInvoiceNo : (p.PurchaseInvoiceUid.HasValue ? "#" + p.PurchaseInvoiceUid : "—"),
                Amount = p.Amount,
                ReferenceNo = p.ReferenceNo
            }).ToListAsync(ct);
        return View(rows);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadLookups(ct);
        return View(new VendorPaymentFormViewModel
        {
            PaymentDate = DateTime.Now,
            PaymentNo = $"VP-{DateTime.Now:yyyyMMddHHmmss}"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorPaymentFormViewModel model, CancellationToken ct)
    {
        ValidatePayment(model);
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var e = new VendorPayment
            {
                PaymentNo = model.PaymentNo.Trim(),
                PaymentDate = model.PaymentDate,
                VendorUid = model.VendorUid,
                PurchaseInvoiceUid = model.PurchaseInvoiceUid,
                PaymentMethodUid = model.PaymentMethodUid,
                Amount = model.Amount,
                ReferenceNo = model.ReferenceNo,
                Remarks = model.Remarks,
                CreatedDate = DateTime.Now
            };
            _db.VendorPayments.Add(e);
            await _db.SaveChangesAsync(ct);

            if (model.PurchaseInvoiceUid is int piUid)
            {
                await ApplyToPurchaseInvoiceAsync(piUid, model.Amount, ct);
            }

            await _audit.WriteAsync("Payment", "VendorPayments", e.Uid.ToString(),
                $"Vendor payment {e.PaymentNo}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadLookups(ct);
            return View(model);
        }

        TempData["Success"] = "Vendor payment saved.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var e = await _db.VendorPayments.FindAsync([id], ct);
        if (e is null) return NotFound();
        await LoadLookups(ct);
        return View(new VendorPaymentFormViewModel
        {
            Uid = e.Uid,
            PaymentNo = e.PaymentNo,
            PaymentDate = e.PaymentDate,
            VendorUid = e.VendorUid,
            PurchaseInvoiceUid = e.PurchaseInvoiceUid,
            PaymentMethodUid = e.PaymentMethodUid,
            Amount = e.Amount,
            ReferenceNo = e.ReferenceNo,
            Remarks = e.Remarks
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VendorPaymentFormViewModel model, CancellationToken ct)
    {
        if (id != model.Uid) return BadRequest();
        ValidatePayment(model);
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var e = await _db.VendorPayments.FindAsync([id], ct);
            if (e is null) return NotFound();

            var prevInvoice = e.PurchaseInvoiceUid;
            var prevAmount = e.Amount;

            if (prevInvoice is int oldPi)
            {
                await ApplyToPurchaseInvoiceAsync(oldPi, -prevAmount, ct);
            }

            e.PaymentNo = model.PaymentNo.Trim();
            e.PaymentDate = model.PaymentDate;
            e.VendorUid = model.VendorUid;
            e.PurchaseInvoiceUid = model.PurchaseInvoiceUid;
            e.PaymentMethodUid = model.PaymentMethodUid;
            e.Amount = model.Amount;
            e.ReferenceNo = model.ReferenceNo;
            e.Remarks = model.Remarks;
            await _db.SaveChangesAsync(ct);

            if (model.PurchaseInvoiceUid is int newPi)
            {
                await ApplyToPurchaseInvoiceAsync(newPi, model.Amount, ct);
            }

            await _audit.WriteAsync("Update", "VendorPayments", e.Uid.ToString(),
                $"Updated payment {e.PaymentNo}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadLookups(ct);
            return View(model);
        }

        TempData["Success"] = "Vendor payment updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var e = await _db.VendorPayments.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    private void ValidatePayment(VendorPaymentFormViewModel model)
    {
        if (model.Amount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "Amount must be greater than zero.");
        }
    }

    private async Task ApplyToPurchaseInvoiceAsync(int purchaseInvoiceUid, decimal delta, CancellationToken ct)
    {
        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(x => x.Uid == purchaseInvoiceUid, ct)
            ?? throw new InvalidOperationException("Purchase invoice was not found.");

        invoice.PaidAmount = Math.Max(0, invoice.PaidAmount + delta);
        if (invoice.PaidAmount > invoice.NetAmount)
        {
            invoice.PaidAmount = invoice.NetAmount;
        }

        invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        invoice.PaymentStatus = invoice.PaidAmount <= 0
            ? "Unpaid"
            : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
        invoice.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
    }
}
