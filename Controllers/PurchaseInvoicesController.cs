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
public class PurchaseInvoicesController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly IInventoryLedgerService _inventory;
    private readonly INavContextService _navContext;

    public PurchaseInvoicesController(
        POSDbContext db,
        IAuditService audit,
        IInventoryLedgerService inventory,
        INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _inventory = inventory;
        _navContext = navContext;
    }

    private void EnsurePurchasesNav() => _navContext.SetArea("Purchases");

    private async Task LoadLookups(CancellationToken ct)
    {
        ViewBag.Branches = new SelectList(await _db.Branches.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BranchName).ToListAsync(ct), "Uid", "BranchName");
        ViewBag.Warehouses = new SelectList(await _db.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.WarehouseName).ToListAsync(ct), "Uid", "WarehouseName");
        ViewBag.Vendors = new SelectList(await _db.Vendors.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.VendorName).ToListAsync(ct), "Uid", "VendorName");
        ViewBag.Products = new SelectList(await _db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProductName).ToListAsync(ct), "Uid", "ProductName");
    }

    public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsurePurchasesNav();
        ViewData["Title"] = "Purchase Invoices";
        var fromDay = dateFrom?.Date;
        var toExclusive = dateTo?.Date.AddDays(1);

        var query = _db.PurchaseInvoices.AsNoTracking().AsQueryable();
        if (fromDay.HasValue)
            query = query.Where(x => x.PurchaseDate >= fromDay.Value);
        if (toExclusive.HasValue)
            query = query.Where(x => x.PurchaseDate < toExclusive.Value);

        ViewBag.DateFrom = fromDay;
        ViewBag.DateTo = dateTo?.Date;

        return View(await query.OrderByDescending(x => x.PurchaseDate).ThenByDescending(x => x.Uid).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsurePurchasesNav();
        await LoadLookups(ct);
        return View(new PurchaseInvoiceFormViewModel
        {
            PurchaseDate = DateTime.Now,
            PurchaseInvoiceNo = $"PI-{DateTime.Now:yyyyMMddHHmmss}",
            Lines = Enumerable.Range(0, 5).Select(_ => new PurchaseInvoiceLineFormViewModel()).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var invalidLines = (model.Lines ?? [])
            .Where(x => x.ProductUid is > 0 && (x.Quantity <= 0 || x.UnitCost < 0))
            .ToList();
        if (invalidLines.Count > 0)
            ModelState.AddModelError(nameof(model.Lines), "Line quantity must be greater than zero and unit cost cannot be negative.");

        var receiveLines = (model.Lines ?? [])
            .Where(x => x.ProductUid is > 0 && x.Quantity > 0 && x.UnitCost >= 0)
            .ToList();

        if (receiveLines.Count > 0 && model.WarehouseUid is null or <= 0)
            ModelState.AddModelError(nameof(model.WarehouseUid), "Select a warehouse when receiving stock lines.");

        var allowedStatus = new[] { "Completed", "Draft", "Cancelled" };
        var allowedPay = new[] { "Unpaid", "Partial", "Paid" };
        if (!allowedStatus.Contains(model.InvoiceStatus))
            ModelState.AddModelError(nameof(model.InvoiceStatus), "Invalid invoice status.");
        if (!allowedPay.Contains(model.PaymentStatus))
            ModelState.AddModelError(nameof(model.PaymentStatus), "Invalid payment status.");

        if (!ModelState.IsValid)
        {
            EnsureLineRows(model);
            await LoadLookups(ct);
            return View(model);
        }

        if (await _db.PurchaseInvoices.AnyAsync(x => x.PurchaseInvoiceNo == model.PurchaseInvoiceNo, ct))
        {
            ModelState.AddModelError(nameof(model.PurchaseInvoiceNo), "Purchase invoice no already exists.");
            EnsureLineRows(model);
            await LoadLookups(ct);
            return View(model);
        }

        var productIds = receiveLines.Select(x => x.ProductUid!.Value).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking()
                .Where(x => productIds.Contains(x.Uid))
                .ToDictionaryAsync(x => x.Uid, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;

        if (receiveLines.Count > 0)
        {
            var sub = receiveLines.Sum(x => Math.Round(x.Quantity * x.UnitCost, 2));
            e.SubTotal = sub;
            e.NetAmount = sub + e.TaxAmount + e.OtherCharges - e.DiscountAmount;
            if (e.NetAmount < 0) e.NetAmount = 0;
            if (e.PaidAmount < 0) e.PaidAmount = 0;
            if (e.PaidAmount > e.NetAmount) e.PaidAmount = e.NetAmount;
            e.BalanceAmount = e.NetAmount - e.PaidAmount;
            e.PaymentStatus = e.PaidAmount <= 0 ? "Unpaid" : e.PaidAmount >= e.NetAmount ? "Paid" : "Partial";
        }

        _db.PurchaseInvoices.Add(e);
        await _db.SaveChangesAsync(ct);

        var movements = new List<InventoryLineMovement>();
        foreach (var line in receiveLines)
        {
            products.TryGetValue(line.ProductUid!.Value, out var product);
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product?.PurchasePrice ?? 0;
            var lineTotal = Math.Round(line.Quantity * unitCost, 2);
            _db.PurchaseInvoiceDetails.Add(new PurchaseInvoiceDetail
            {
                PurchaseInvoiceUid = e.Uid,
                ProductUid = line.ProductUid.Value,
                ProductCode = product?.ProductCode,
                ProductName = product?.ProductName,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                LineTotal = lineTotal
            });
            movements.Add(new InventoryLineMovement
            {
                ProductUid = line.ProductUid.Value,
                ProductName = product?.ProductName ?? line.ProductUid.Value.ToString(),
                Quantity = line.Quantity,
                UnitCost = unitCost
            });
        }

        if (movements.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            var warehouseUid = e.WarehouseUid
                ?? await _inventory.ResolveWarehouseUidAsync(null, e.BranchUid, ct);
            e.WarehouseUid ??= warehouseUid;
            await _inventory.ApplyPurchaseAsync(e.Uid, e.PurchaseInvoiceNo, warehouseUid, movements, e.UserUid, e.TerminalUid, ct);
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync("Create", "PurchaseInvoices", e.Uid.ToString(),
            $"Created purchase {e.PurchaseInvoiceNo} with {movements.Count} stock line(s)", ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = movements.Count > 0
            ? $"Purchase invoice created and {movements.Count} product(s) added to inventory."
            : "Purchase invoice created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.InvoiceStatus == "Cancelled")
        {
            TempData["Error"] = "Cancelled invoices cannot be edited.";
            return RedirectToAction(nameof(Index));
        }
        await LoadLookups(ct);
        var form = ToForm(e);
        EnsureLineRows(form);
        ViewBag.ExistingLines = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == id)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        EnsurePurchasesNav();
        if (id != model.Uid) return BadRequest();

        var receiveLines = (model.Lines ?? [])
            .Where(x => x.ProductUid is > 0 && x.Quantity > 0)
            .ToList();

        if (receiveLines.Count > 0 && model.WarehouseUid is null or <= 0)
            ModelState.AddModelError(nameof(model.WarehouseUid), "Select a warehouse when receiving stock lines.");

        if (!ModelState.IsValid)
        {
            EnsureLineRows(model);
            await LoadLookups(ct);
            ViewBag.ExistingLines = await _db.PurchaseInvoiceDetails.AsNoTracking()
                .Where(x => x.PurchaseInvoiceUid == id)
                .OrderBy(x => x.Uid)
                .ToListAsync(ct);
            return View(model);
        }

        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.InvoiceStatus == "Cancelled")
        {
            TempData["Error"] = "Cancelled invoices cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        var productIds = receiveLines.Select(x => x.ProductUid!.Value).Distinct().ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking()
                .Where(x => productIds.Contains(x.Uid))
                .ToDictionaryAsync(x => x.Uid, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        Apply(e, model);
        e.UpdatedDate = DateTime.Now;

        var movements = new List<InventoryLineMovement>();
        foreach (var line in receiveLines)
        {
            products.TryGetValue(line.ProductUid!.Value, out var product);
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product?.PurchasePrice ?? 0;
            var lineTotal = Math.Round(line.Quantity * unitCost, 2);
            _db.PurchaseInvoiceDetails.Add(new PurchaseInvoiceDetail
            {
                PurchaseInvoiceUid = e.Uid,
                ProductUid = line.ProductUid.Value,
                ProductCode = product?.ProductCode,
                ProductName = product?.ProductName,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                LineTotal = lineTotal
            });
            movements.Add(new InventoryLineMovement
            {
                ProductUid = line.ProductUid.Value,
                ProductName = product?.ProductName ?? line.ProductUid.Value.ToString(),
                Quantity = line.Quantity,
                UnitCost = unitCost
            });
        }

        if (movements.Count > 0)
        {
            if (e.SubTotal <= 0)
            {
                var existingSub = await _db.PurchaseInvoiceDetails.AsNoTracking()
                    .Where(x => x.PurchaseInvoiceUid == e.Uid)
                    .SumAsync(x => x.LineTotal, ct);
                e.SubTotal = existingSub + movements.Sum(x => Math.Round(x.Quantity * x.UnitCost, 2));
            }
            if (e.NetAmount <= 0)
                e.NetAmount = e.SubTotal + e.TaxAmount + e.OtherCharges - e.DiscountAmount;
            e.BalanceAmount = e.NetAmount - e.PaidAmount;

            await _db.SaveChangesAsync(ct);
            var warehouseUid = e.WarehouseUid
                ?? await _inventory.ResolveWarehouseUidAsync(null, e.BranchUid, ct);
            e.WarehouseUid ??= warehouseUid;
            await _inventory.ApplyPurchaseAsync(
                e.Uid, e.PurchaseInvoiceNo, warehouseUid, movements, e.UserUid, e.TerminalUid, ct,
                allowAdditional: true);
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "PurchaseInvoices", e.Uid.ToString(),
            $"Updated purchase {e.PurchaseInvoiceNo}" + (movements.Count > 0 ? $" and added {movements.Count} stock line(s)" : ""), ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = movements.Count > 0
            ? $"Purchase invoice updated and {movements.Count} product(s) added to inventory."
            : "Purchase invoice updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var e = await _db.PurchaseInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (e is null) return NotFound();

        ViewBag.Lines = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == id)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);
        return View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (string.Equals(e.InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Purchase invoice is already cancelled.";
            return RedirectToAction(nameof(Index));
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            e.InvoiceStatus = "Cancelled";
            e.UpdatedDate = DateTime.Now;
            await _inventory.ReversePurchaseAsync(e.Uid, e.PurchaseInvoiceNo, e.UserUid, e.TerminalUid, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Cancel", "PurchaseInvoices", e.Uid.ToString(), $"Cancelled purchase {e.PurchaseInvoiceNo}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = "Cancel failed: " + ex.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = "Purchase invoice cancelled and stock reversed.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (invoice is null)
        {
            TempData["Error"] = "Purchase invoice was not found.";
            return RedirectToAction(nameof(Index));
        }

        var invoiceNo = invoice.PurchaseInvoiceNo;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await _inventory.ReversePurchaseAsync(invoice.Uid, invoice.PurchaseInvoiceNo, invoice.UserUid, invoice.TerminalUid, ct);

            var details = await _db.PurchaseInvoiceDetails.Where(x => x.PurchaseInvoiceUid == id).ToListAsync(ct);
            if (details.Count > 0)
                _db.PurchaseInvoiceDetails.RemoveRange(details);

            var payments = await _db.VendorPayments.Where(x => x.PurchaseInvoiceUid == id).ToListAsync(ct);
            foreach (var payment in payments)
                payment.PurchaseInvoiceUid = null;

            _db.PurchaseInvoices.Remove(invoice);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "PurchaseInvoices", id.ToString(),
                $"Deleted purchase invoice {invoiceNo}, {details.Count} detail line(s), unlinked {payments.Count} vendor payment(s).", ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Cannot delete purchase invoice {invoiceNo} because other records still use it.";
            return RedirectToAction(nameof(Index));
        }

        TempData["Success"] = $"Purchase invoice {invoiceNo} deleted.";
        return RedirectToAction(nameof(Index));
    }

    private static void EnsureLineRows(PurchaseInvoiceFormViewModel model)
    {
        model.Lines ??= [];
        while (model.Lines.Count < 5)
            model.Lines.Add(new PurchaseInvoiceLineFormViewModel());
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
