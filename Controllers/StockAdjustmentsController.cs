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
public class StockAdjustmentsController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public StockAdjustmentsController(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    private async Task LoadLookups(CancellationToken ct)
    {
        ViewBag.Branches = new SelectList(
            await _db.Branches.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.BranchName).ToListAsync(ct),
            "Uid", "BranchName");
        ViewBag.Warehouses = new SelectList(
            await _db.Warehouses.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.WarehouseName).ToListAsync(ct),
            "Uid", "WarehouseName");
        ViewBag.Products = new SelectList(
            await _db.Products.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.ProductName).ToListAsync(ct),
            "Uid", "ProductName");
    }

    public async Task<IActionResult> Index(CancellationToken ct) =>
        View(await _db.StockAdjustments.AsNoTracking().OrderByDescending(x => x.AdjustmentDate).ToListAsync(ct));

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        await LoadLookups(ct);
        return View(new StockAdjustmentFormViewModel
        {
            AdjustmentDate = DateTime.Now,
            AdjustmentNo = $"SA-{DateTime.Now:yyyyMMddHHmmss}"
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockAdjustmentFormViewModel model, CancellationToken ct)
    {
        if (model.ProductUid <= 0)
            ModelState.AddModelError(nameof(model.ProductUid), "Product is required.");
        if (model.Quantity <= 0)
            ModelState.AddModelError(nameof(model.Quantity), "Quantity must be greater than zero.");
        if (!string.Equals(model.Direction, "Increase", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(model.Direction, "Decrease", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(model.Direction), "Direction must be Increase or Decrease.");

        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var e = new StockAdjustment
            {
                AdjustmentNo = model.AdjustmentNo.Trim(),
                AdjustmentDate = model.AdjustmentDate,
                BranchUid = model.BranchUid,
                WarehouseUid = model.WarehouseUid,
                AdjustmentType = model.AdjustmentType,
                Remarks = model.Remarks,
                Status = model.Status,
                CreatedDate = DateTime.Now
            };
            _db.StockAdjustments.Add(e);
            await _db.SaveChangesAsync(ct);

            var signedQty = string.Equals(model.Direction, "Decrease", StringComparison.OrdinalIgnoreCase)
                ? -model.Quantity
                : model.Quantity;

            var inv = await _db.Inventories.FirstOrDefaultAsync(
                x => x.ProductUid == model.ProductUid && x.WarehouseUid == model.WarehouseUid, ct);
            if (inv is null)
            {
                inv = new Inventory
                {
                    ProductUid = model.ProductUid,
                    WarehouseUid = model.WarehouseUid,
                    QuantityIn = 0,
                    QuantityOut = 0,
                    CurrentQuantity = 0,
                    LastStockDate = DateTime.Now,
                    UpdatedDate = DateTime.Now
                };
                _db.Inventories.Add(inv);
                await _db.SaveChangesAsync(ct);
            }

            if (signedQty < 0 && inv.CurrentQuantity + signedQty < 0)
                throw new InvalidOperationException("Adjustment would make stock negative.");

            if (signedQty >= 0)
            {
                inv.QuantityIn += signedQty;
                inv.CurrentQuantity += signedQty;
            }
            else
            {
                var outQty = Math.Abs(signedQty);
                inv.QuantityOut += outQty;
                inv.CurrentQuantity -= outQty;
            }

            inv.LastStockDate = DateTime.Now;
            inv.UpdatedDate = DateTime.Now;

            _db.StockAdjustmentDetails.Add(new StockAdjustmentDetail
            {
                StockAdjustmentUid = e.Uid,
                ProductUid = model.ProductUid,
                SystemQuantity = inv.CurrentQuantity - signedQty,
                PhysicalQuantity = inv.CurrentQuantity,
                AdjustmentQuantity = signedQty
            });

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = DateTime.Now,
                ProductUid = model.ProductUid,
                WarehouseUid = model.WarehouseUid,
                TransactionType = "Adjustment",
                ReferenceUid = e.Uid,
                ReferenceNo = e.AdjustmentNo,
                QuantityIn = signedQty > 0 ? signedQty : 0,
                QuantityOut = signedQty < 0 ? Math.Abs(signedQty) : 0,
                BalanceQuantity = inv.CurrentQuantity,
                Remarks = $"{model.Direction} {model.Quantity:0.###}",
                CreatedDate = DateTime.Now
            });

            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "StockAdjustments", e.Uid.ToString(),
                $"Created adjustment {e.AdjustmentNo}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            await LoadLookups(ct);
            return View(model);
        }

        TempData["Success"] = "Stock adjustment created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var e = await _db.StockAdjustments.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.Status == "Cancelled")
        {
            TempData["Error"] = "Cancelled adjustments cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        await LoadLookups(ct);
        return View(new StockAdjustmentFormViewModel
        {
            Uid = e.Uid,
            AdjustmentNo = e.AdjustmentNo,
            AdjustmentDate = e.AdjustmentDate,
            BranchUid = e.BranchUid,
            WarehouseUid = e.WarehouseUid,
            AdjustmentType = e.AdjustmentType,
            Remarks = e.Remarks,
            Status = e.Status
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StockAdjustmentFormViewModel model, CancellationToken ct)
    {
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        var e = await _db.StockAdjustments.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.AdjustmentNo = model.AdjustmentNo.Trim();
        e.AdjustmentDate = model.AdjustmentDate;
        e.BranchUid = model.BranchUid;
        e.WarehouseUid = model.WarehouseUid;
        e.AdjustmentType = model.AdjustmentType;
        e.Remarks = model.Remarks;
        e.Status = model.Status;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "StockAdjustments", e.Uid.ToString(),
            $"Updated adjustment {e.AdjustmentNo}", ct);
        TempData["Success"] = "Stock adjustment updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var e = await _db.StockAdjustments.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var e = await _db.StockAdjustments.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.Status = "Cancelled";
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "StockAdjustments", e.Uid.ToString(),
            $"Cancelled {e.AdjustmentNo}", ct);
        TempData["Success"] = "Stock adjustment cancelled.";
        return RedirectToAction(nameof(Index));
    }
}
