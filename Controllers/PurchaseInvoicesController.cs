using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class PurchaseInvoicesController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public PurchaseInvoicesController(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private async Task LoadLookups(CancellationToken ct)
    {
        ViewBag.Branches = new SelectList(await _db.Branches.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BranchName).ToListAsync(ct), "Uid", "BranchName");
        ViewBag.Warehouses = new SelectList(await _db.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.WarehouseName).ToListAsync(ct), "Uid", "WarehouseName");
        ViewBag.Vendors = new SelectList(await _db.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.VendorName).ToListAsync(ct), "Uid", "VendorName");
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _db.PurchaseInvoices.AsNoTracking().OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.Uid).ToListAsync(ct));

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadLookups(ct);
        return View(new PurchaseInvoiceFormViewModel
        {
            PurchaseDate = DateTime.Now,
            PurchaseInvoiceNo = $"PI-{DateTime.Now:yyyyMMddHHmmss}"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        if (await _db.PurchaseInvoices.AnyAsync(x => x.PurchaseInvoiceNo == model.PurchaseInvoiceNo, ct))
        {
            ModelState.AddModelError(nameof(model.PurchaseInvoiceNo), "Purchase invoice no already exists.");
            await LoadLookups(ct);
            return View(model);
        }

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;
        _db.PurchaseInvoices.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "PurchaseInvoices", e.Uid.ToString(), $"Created purchase {e.PurchaseInvoiceNo}", ct);
        TempData["Success"] = "Purchase invoice created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.InvoiceStatus == "Cancelled")
        {
            TempData["Error"] = "Cancelled invoices cannot be edited.";
            return RedirectToAction(nameof(Index));
        }
        await LoadLookups(ct);
        return View(ToForm(e));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.InvoiceStatus == "Cancelled")
        {
            TempData["Error"] = "Cancelled invoices cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        Apply(e, model);
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "PurchaseInvoices", e.Uid.ToString(), $"Updated purchase {e.PurchaseInvoiceNo}", ct);
        TempData["Success"] = "Purchase invoice updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var e = await _db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.InvoiceStatus = "Cancelled";
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "PurchaseInvoices", e.Uid.ToString(), $"Cancelled purchase {e.PurchaseInvoiceNo}", ct);
        TempData["Success"] = "Purchase invoice cancelled.";
        return RedirectToAction(nameof(Index));
    }

    private static PurchaseInvoiceFormViewModel ToForm(PurchaseInvoice e) => new()
    {
        Uid = e.Uid,
        PurchaseInvoiceNo = e.PurchaseInvoiceNo,
        VendorInvoiceNo = e.VendorInvoiceNo,
        PurchaseDate = e.PurchaseDate,
        BranchUid = e.BranchUid,
        WarehouseUid = e.WarehouseUid,
        VendorUid = e.VendorUid,
        SubTotal = e.SubTotal,
        DiscountAmount = e.DiscountAmount,
        TaxAmount = e.TaxAmount,
        OtherCharges = e.OtherCharges,
        NetAmount = e.NetAmount,
        PaidAmount = e.PaidAmount,
        BalanceAmount = e.BalanceAmount,
        PaymentStatus = e.PaymentStatus,
        InvoiceStatus = e.InvoiceStatus,
        Remarks = e.Remarks
    };

    private static PurchaseInvoice ToEntity(PurchaseInvoiceFormViewModel m) => new()
    {
        PurchaseInvoiceNo = m.PurchaseInvoiceNo.Trim(),
        VendorInvoiceNo = m.VendorInvoiceNo,
        PurchaseDate = m.PurchaseDate,
        BranchUid = m.BranchUid,
        WarehouseUid = m.WarehouseUid,
        VendorUid = m.VendorUid,
        SubTotal = m.SubTotal,
        DiscountAmount = m.DiscountAmount,
        TaxAmount = m.TaxAmount,
        OtherCharges = m.OtherCharges,
        NetAmount = m.NetAmount,
        PaidAmount = m.PaidAmount,
        BalanceAmount = m.BalanceAmount,
        PaymentStatus = m.PaymentStatus,
        InvoiceStatus = m.InvoiceStatus,
        Remarks = m.Remarks
    };

    private static void Apply(PurchaseInvoice e, PurchaseInvoiceFormViewModel m)
    {
        e.PurchaseInvoiceNo = m.PurchaseInvoiceNo.Trim();
        e.VendorInvoiceNo = m.VendorInvoiceNo;
        e.PurchaseDate = m.PurchaseDate;
        e.BranchUid = m.BranchUid;
        e.WarehouseUid = m.WarehouseUid;
        e.VendorUid = m.VendorUid;
        e.SubTotal = m.SubTotal;
        e.DiscountAmount = m.DiscountAmount;
        e.TaxAmount = m.TaxAmount;
        e.OtherCharges = m.OtherCharges;
        e.NetAmount = m.NetAmount;
        e.PaidAmount = m.PaidAmount;
        e.BalanceAmount = m.BalanceAmount;
        e.PaymentStatus = m.PaymentStatus;
        e.InvoiceStatus = m.InvoiceStatus;
        e.Remarks = m.Remarks;
    }
}
