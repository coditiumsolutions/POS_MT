using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class PurchaseInvoicesController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly IInventoryLedgerService _inventory;
    private readonly IYogurtRecipeService _yogurtRecipe;
    private readonly INavContextService _navContext;

    public PurchaseInvoicesController(
        POSDbContext db,
        IAuditService audit,
        IInventoryLedgerService inventory,
        IYogurtRecipeService yogurtRecipe,
        INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _inventory = inventory;
        _yogurtRecipe = yogurtRecipe;
        _navContext = navContext;
    }

    private void EnsurePurchasesNav() => _navContext.SetArea("Purchases");

    private async Task LoadLookups(
        CancellationToken ct,
        int? selectedVendorUid = null,
        int? selectedBranchUid = null,
        int? selectedWarehouseUid = null,
        IEnumerable<int>? selectedProductUids = null)
    {
        var branches = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive || (selectedBranchUid != null && x.Uid == selectedBranchUid))
            .OrderBy(x => x.BranchName)
            .ToListAsync(ct);
        var warehouses = await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsActive || (selectedWarehouseUid != null && x.Uid == selectedWarehouseUid))
            .OrderBy(x => x.WarehouseName)
            .Select(x => new
            {
                x.Uid,
                x.WarehouseName,
                BranchUid = x.BranchUid ?? 0
            })
            .ToListAsync(ct);
        var vendors = await _db.Vendors.AsNoTracking()
            .Where(x => x.IsActive || (selectedVendorUid != null && x.Uid == selectedVendorUid))
            .OrderBy(x => x.VendorName)
            .ToListAsync(ct);

        ViewBag.Branches = new SelectList(branches, "Uid", "BranchName", selectedBranchUid);
        ViewBag.Warehouses = new SelectList(warehouses, "Uid", "WarehouseName", selectedWarehouseUid);
        ViewBag.WarehouseBranchMapJson = System.Text.Json.JsonSerializer.Serialize(
            warehouses.Select(x => new { uid = x.Uid, branchUid = x.BranchUid }));
        ViewBag.Vendors = new SelectList(vendors, "Uid", "VendorName", selectedVendorUid);

        var keepProductIds = (selectedProductUids ?? [])
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        // Plain list (not a shared SelectList) — reused SelectList in line loops mutates Selected and shows wrong options.
        ViewBag.ProductOptions = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive || keepProductIds.Contains(x.Uid))
            .OrderBy(x => x.ProductName)
            .Select(x => new PurchaseProductOptionViewModel
            {
                Uid = x.Uid,
                Label = x.ProductCode + " — " + x.ProductName,
                SalePrice = x.SalePrice
            })
            .ToListAsync(ct);
    }

    public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsurePurchasesNav();
        ViewData["Title"] = "Purchase Invoices";
        var fromDay = dateFrom?.Date;
        var toExclusive = dateTo?.Date.AddDays(1);

        var query =
            from inv in _db.PurchaseInvoices.AsNoTracking()
            join v in _db.Vendors.AsNoTracking() on inv.VendorUid equals v.Uid into vj
            from v in vj.DefaultIfEmpty()
            select new { inv, VendorName = v != null ? v.VendorName : null };

        if (fromDay.HasValue)
            query = query.Where(x => x.inv.PurchaseDate >= fromDay.Value);
        if (toExclusive.HasValue)
            query = query.Where(x => x.inv.PurchaseDate < toExclusive.Value);

        ViewBag.DateFrom = fromDay;
        ViewBag.DateTo = dateTo?.Date;

        var rows = await query
            .OrderByDescending(x => x.inv.PurchaseDate)
            .ThenByDescending(x => x.inv.Uid)
            .Select(x => new PurchaseInvoiceListItemViewModel
            {
                Uid = x.inv.Uid,
                PurchaseInvoiceNo = x.inv.PurchaseInvoiceNo,
                VendorInvoiceNo = x.inv.VendorInvoiceNo,
                VendorName = x.VendorName ?? ("#" + x.inv.VendorUid.ToString()),
                PurchaseDate = x.inv.PurchaseDate,
                NetAmount = x.inv.NetAmount,
                PaidAmount = x.inv.PaidAmount,
                InvoiceStatus = x.inv.InvoiceStatus,
                PaymentStatus = x.inv.PaymentStatus
            })
            .ToListAsync(ct);

        return View(rows);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsurePurchasesNav();
        await LoadLookups(ct);
        ViewBag.VendorInvoiceNoReadonly = true;
        var now = DateTime.Now;
        return View(new PurchaseInvoiceFormViewModel
        {
            PurchaseDate = now,
            PurchaseInvoiceNo = $"PI-{now:yyyyMMddHHmmss}",
            VendorInvoiceNo = await GenerateNextVendorInvoiceNoAsync(now, ct),
            Lines = Enumerable.Range(0, 15).Select(_ => new PurchaseInvoiceLineFormViewModel()).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        EnsurePurchasesNav();
        ViewBag.VendorInvoiceNoReadonly = true;

        // Always assign the next Ven+ddMMyyyy.n number on create (auto).
        model.VendorInvoiceNo = await GenerateNextVendorInvoiceNoAsync(
            model.PurchaseDate == default ? DateTime.Now : model.PurchaseDate, ct);

        ValidatePurchaseRequiredFields(model, requireReceiveLines: true);

        var invalidLines = (model.Lines ?? [])
            .Where(x => x.ProductUid is > 0 && (x.Quantity <= 0 || x.UnitCost < 0))
            .ToList();
        if (invalidLines.Count > 0)
            ModelState.AddModelError(nameof(model.Lines), "Line quantity must be greater than zero and unit cost cannot be negative.");

        var receiveLines = (model.Lines ?? [])
            .Where(x => x.ProductUid is > 0 && x.Quantity > 0 && x.UnitCost >= 0)
            .ToList();

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
        if (e.PurchaseDate == default) e.PurchaseDate = DateTime.Now;
        else if (e.PurchaseDate.TimeOfDay == TimeSpan.Zero) e.PurchaseDate = e.PurchaseDate.Date.Add(DateTime.Now.TimeOfDay);
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
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product?.SalePrice ?? 0;
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

        string? yogurtMsg = null;
        if (movements.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            // Always resolve against the purchase branch so stock cannot post to another branch's warehouse.
            var warehouseUid = await _inventory.ResolveWarehouseUidAsync(e.WarehouseUid, e.BranchUid, ct);
            e.WarehouseUid = warehouseUid;
            await _inventory.ApplyPurchaseAsync(e.Uid, e.PurchaseInvoiceNo, warehouseUid, movements, e.UserUid, e.TerminalUid, ct);
            await _db.SaveChangesAsync(ct);

            yogurtMsg = await ApplyYogurtMilkConsumptionAsync(
                movements, warehouseUid, e.BranchUid, e.UserUid, e.TerminalUid, e.PurchaseInvoiceNo, ct);
        }

        await _audit.WriteAsync("Create", "PurchaseInvoices", e.Uid.ToString(),
            $"Created purchase {e.PurchaseInvoiceNo} with {movements.Count} stock line(s)", ct);
        await tx.CommitAsync(ct);

        TempData["Success"] = movements.Count > 0
            ? $"Purchase invoice created and {movements.Count} product(s) added to inventory."
              + (string.IsNullOrEmpty(yogurtMsg) ? "" : " " + yogurtMsg)
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
        var form = ToForm(e);
        await PopulateExistingPurchaseLinesAsync(form, id, ct);
        await LoadLookups(ct, form.VendorUid, form.BranchUid, form.WarehouseUid,
            form.Lines.Where(x => x.ProductUid is > 0).Select(x => x.ProductUid!.Value));
        EnsureLineRows(form);
        await LoadVendorPaymentContextAsync(id, ct);
        return View(form);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, PurchaseInvoiceFormViewModel model, CancellationToken ct)
    {
        EnsurePurchasesNav();
        if (id != model.Uid) return BadRequest();

        var postedLines = model.Lines ?? [];
        var newReceiveLines = postedLines
            .Where(x => x.ExistingDetailUid is null && x.ProductUid is > 0 && x.Quantity > 0)
            .ToList();
        var editedExistingLines = postedLines
            .Where(x => x.ExistingDetailUid is > 0 && x.ProductUid is > 0 && x.Quantity > 0)
            .ToList();

        ValidatePurchaseRequiredFields(model, requireReceiveLines: false);

        if (!ModelState.IsValid)
        {
            EnsureLineRows(model);
            await LoadLookups(ct, model.VendorUid, model.BranchUid, model.WarehouseUid,
                model.Lines.Where(x => x.ProductUid is > 0).Select(x => x.ProductUid!.Value));
            await LoadVendorPaymentContextAsync(id, ct);
            return View(model);
        }

        var e = await _db.PurchaseInvoices.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (e.InvoiceStatus == "Cancelled")
        {
            TempData["Error"] = "Cancelled invoices cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        var productIds = editedExistingLines.Select(x => x.ProductUid!.Value)
            .Concat(newReceiveLines.Select(x => x.ProductUid!.Value))
            .Distinct()
            .ToList();
        var products = productIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking()
                .Where(x => productIds.Contains(x.Uid))
                .ToDictionaryAsync(x => x.Uid, ct);

        var existingDetails = await _db.PurchaseInvoiceDetails
            .Where(x => x.PurchaseInvoiceUid == id)
            .ToListAsync(ct);
        var existingByUid = existingDetails.ToDictionary(x => x.Uid);

        // Load product names for inventory deltas on old products when the line product changes.
        var oldProductIds = existingDetails.Select(x => x.ProductUid).Distinct().ToList();
        var oldProducts = oldProductIds.Count == 0
            ? new Dictionary<int, Product>()
            : await _db.Products.AsNoTracking()
                .Where(x => oldProductIds.Contains(x.Uid))
                .ToDictionaryAsync(x => x.Uid, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        Apply(e, model);
        e.UpdatedDate = DateTime.Now;

        var warehouseUid = await _inventory.ResolveWarehouseUidAsync(e.WarehouseUid, e.BranchUid, ct);
        e.WarehouseUid = warehouseUid;

        var editedCount = 0;
        var yogurtMovements = new List<InventoryLineMovement>();

        foreach (var line in editedExistingLines)
        {
            if (!existingByUid.TryGetValue(line.ExistingDetailUid!.Value, out var detail))
                continue;

            products.TryGetValue(line.ProductUid!.Value, out var product);
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product?.SalePrice ?? detail.UnitCost;
            var newQty = line.Quantity;
            var newProductUid = line.ProductUid.Value;
            var oldProductUid = detail.ProductUid;
            var oldQty = detail.Quantity;
            var oldUnitCost = detail.UnitCost;

            var productChanged = newProductUid != oldProductUid;
            var qtyChanged = newQty != oldQty;
            var costChanged = unitCost != oldUnitCost;
            if (!productChanged && !qtyChanged && !costChanged)
                continue;

            if (productChanged)
            {
                oldProducts.TryGetValue(oldProductUid, out var oldProduct);
                await _inventory.ApplyPurchaseQuantityDeltaAsync(
                    e.Uid,
                    e.PurchaseInvoiceNo,
                    warehouseUid,
                    new InventoryLineMovement
                    {
                        ProductUid = oldProductUid,
                        ProductName = oldProduct?.ProductName ?? detail.ProductName ?? oldProductUid.ToString(),
                        Quantity = -oldQty,
                        UnitCost = oldUnitCost
                    },
                    e.UserUid,
                    e.TerminalUid,
                    ct);

                await _inventory.ApplyPurchaseQuantityDeltaAsync(
                    e.Uid,
                    e.PurchaseInvoiceNo,
                    warehouseUid,
                    new InventoryLineMovement
                    {
                        ProductUid = newProductUid,
                        ProductName = product?.ProductName ?? newProductUid.ToString(),
                        Quantity = newQty,
                        UnitCost = unitCost
                    },
                    e.UserUid,
                    e.TerminalUid,
                    ct);

                yogurtMovements.Add(new InventoryLineMovement
                {
                    ProductUid = newProductUid,
                    ProductName = product?.ProductName ?? newProductUid.ToString(),
                    Quantity = newQty,
                    UnitCost = unitCost
                });
            }
            else if (qtyChanged)
            {
                var delta = newQty - oldQty;
                await _inventory.ApplyPurchaseQuantityDeltaAsync(
                    e.Uid,
                    e.PurchaseInvoiceNo,
                    warehouseUid,
                    new InventoryLineMovement
                    {
                        ProductUid = newProductUid,
                        ProductName = product?.ProductName ?? detail.ProductName ?? newProductUid.ToString(),
                        Quantity = delta,
                        UnitCost = unitCost
                    },
                    e.UserUid,
                    e.TerminalUid,
                    ct);

                if (delta > 0)
                {
                    yogurtMovements.Add(new InventoryLineMovement
                    {
                        ProductUid = newProductUid,
                        ProductName = product?.ProductName ?? newProductUid.ToString(),
                        Quantity = delta,
                        UnitCost = unitCost
                    });
                }
            }

            detail.ProductUid = newProductUid;
            detail.ProductCode = product?.ProductCode ?? detail.ProductCode;
            detail.ProductName = product?.ProductName ?? detail.ProductName;
            detail.Quantity = newQty;
            detail.UnitCost = unitCost;
            detail.LineTotal = Math.Round(newQty * unitCost, 2);
            editedCount++;
        }

        var newMovements = new List<InventoryLineMovement>();
        foreach (var line in newReceiveLines)
        {
            products.TryGetValue(line.ProductUid!.Value, out var product);
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product?.SalePrice ?? 0;
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
            newMovements.Add(new InventoryLineMovement
            {
                ProductUid = line.ProductUid.Value,
                ProductName = product?.ProductName ?? line.ProductUid.Value.ToString(),
                Quantity = line.Quantity,
                UnitCost = unitCost
            });
        }

        string? yogurtMsg = null;
        if (newMovements.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _inventory.ApplyPurchaseAsync(
                e.Uid, e.PurchaseInvoiceNo, warehouseUid, newMovements, e.UserUid, e.TerminalUid, ct,
                allowAdditional: true);
            yogurtMovements.AddRange(newMovements);
        }

        if (yogurtMovements.Count > 0)
        {
            yogurtMsg = await ApplyYogurtMilkConsumptionAsync(
                yogurtMovements, warehouseUid, e.BranchUid, e.UserUid, e.TerminalUid, e.PurchaseInvoiceNo, ct);
        }

        await RecalculatePurchaseTotalsAsync(e, ct);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "PurchaseInvoices", e.Uid.ToString(),
            $"Updated purchase {e.PurchaseInvoiceNo}"
            + (editedCount > 0 ? $", edited {editedCount} line(s)" : "")
            + (newMovements.Count > 0 ? $", added {newMovements.Count} stock line(s)" : ""), ct);
        await tx.CommitAsync(ct);

        var parts = new List<string> { "Purchase invoice updated." };
        if (editedCount > 0) parts.Add($"{editedCount} existing line(s) changed.");
        if (newMovements.Count > 0) parts.Add($"{newMovements.Count} product(s) added to inventory.");
        if (!string.IsNullOrEmpty(yogurtMsg)) parts.Add(yogurtMsg);
        TempData["Success"] = string.Join(" ", parts);
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayVendor(
        int id,
        decimal amount,
        int? paymentMethodUid,
        DateTime paymentDate,
        string? referenceNo,
        CancellationToken ct)
    {
        EnsurePurchasesNav();
        var invoice = await _db.PurchaseInvoices.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (invoice is null) return NotFound();
        if (string.Equals(invoice.InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            TempData["PaymentError"] = "Cancelled invoices cannot accept payments.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await RecalculatePurchaseTotalsAsync(invoice, ct);
        await _db.SaveChangesAsync(ct);

        if (amount <= 0)
        {
            TempData["PaymentError"] = "Payment amount must be greater than zero.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (invoice.BalanceAmount <= 0)
        {
            TempData["PaymentError"] = "This invoice has no remaining balance.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        if (amount > invoice.BalanceAmount)
            amount = invoice.BalanceAmount;

        var payDate = paymentDate == default ? DateTime.Now : paymentDate;

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var payment = new VendorPayment
            {
                PaymentNo = $"VP-{DateTime.Now:yyyyMMddHHmmss}",
                PaymentDate = payDate,
                VendorUid = invoice.VendorUid,
                PurchaseInvoiceUid = invoice.Uid,
                PaymentMethodUid = paymentMethodUid is > 0 ? paymentMethodUid : null,
                Amount = amount,
                ReferenceNo = string.IsNullOrWhiteSpace(referenceNo) ? null : referenceNo.Trim(),
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
                $"Vendor payment {payment.PaymentNo} for purchase {invoice.PurchaseInvoiceNo}: {amount:N2}", ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["PaymentError"] = "Payment failed: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["PaymentSuccess"] = $"Paid {amount:N2} to vendor. Remaining balance: {invoice.BalanceAmount:N2}.";
        return RedirectToAction(nameof(Edit), new { id });
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

    /// <summary>Printable purchase invoice report (browser Print / Save as PDF).</summary>
    public async Task<IActionResult> Print(int id, CancellationToken ct)
    {
        EnsurePurchasesNav();
        var model = await LoadPrintModelAsync(id, ct);
        if (model is null) return NotFound();
        return View(model);
    }

    private async Task<PurchaseInvoicePrintViewModel?> LoadPrintModelAsync(int id, CancellationToken ct)
    {
        var row = await (
            from inv in _db.PurchaseInvoices.AsNoTracking()
            join v in _db.Vendors.AsNoTracking() on inv.VendorUid equals v.Uid into vj
            from v in vj.DefaultIfEmpty()
            join b in _db.Branches.AsNoTracking() on inv.BranchUid equals b.Uid into bj
            from b in bj.DefaultIfEmpty()
            join w in _db.Warehouses.AsNoTracking() on inv.WarehouseUid equals w.Uid into wj
            from w in wj.DefaultIfEmpty()
            where inv.Uid == id
            select new { inv, v, b, w }
        ).FirstOrDefaultAsync(ct);

        if (row is null) return null;

        var lines = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == id)
            .OrderBy(x => x.Uid)
            .Select(x => new PurchaseInvoicePrintLineViewModel
            {
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost,
                DiscountAmount = x.DiscountAmount,
                TaxAmount = x.TaxAmount,
                LineTotal = x.LineTotal,
                BatchNo = x.BatchNo
            })
            .ToListAsync(ct);

        var payments = await _db.VendorPayments.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == id)
            .OrderBy(x => x.PaymentDate)
            .ThenBy(x => x.Uid)
            .Select(x => new PurchaseInvoicePrintPaymentViewModel
            {
                PaymentNo = x.PaymentNo,
                PaymentDate = x.PaymentDate,
                Amount = x.Amount,
                ReferenceNo = x.ReferenceNo,
                Remarks = x.Remarks
            })
            .ToListAsync(ct);

        return new PurchaseInvoicePrintViewModel
        {
            Uid = row.inv.Uid,
            PurchaseInvoiceNo = row.inv.PurchaseInvoiceNo,
            VendorInvoiceNo = row.inv.VendorInvoiceNo,
            PurchaseDate = row.inv.PurchaseDate,
            VendorCode = row.v?.VendorCode ?? string.Empty,
            VendorName = row.v?.VendorName ?? $"Vendor #{row.inv.VendorUid}",
            VendorMobile = row.v?.MobileNo ?? row.v?.PhoneNo,
            BranchName = row.b?.BranchName,
            WarehouseName = row.w?.WarehouseName,
            InvoiceStatus = row.inv.InvoiceStatus,
            PaymentStatus = row.inv.PaymentStatus,
            Remarks = row.inv.Remarks,
            SubTotal = row.inv.SubTotal,
            DiscountAmount = row.inv.DiscountAmount,
            TaxAmount = row.inv.TaxAmount,
            OtherCharges = row.inv.OtherCharges,
            NetAmount = row.inv.NetAmount,
            PaidAmount = row.inv.PaidAmount,
            BalanceAmount = row.inv.BalanceAmount,
            PrintedAt = DateTime.Now,
            Lines = lines,
            Payments = payments
        };
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

    /// <summary>
    /// Format: VEN-ddMMyyyy-n (VEN-03112026-1, VEN-03112026-2, …).
    /// </summary>
    private async Task<string> GenerateNextVendorInvoiceNoAsync(DateTime date, CancellationToken ct)
    {
        var day = date == default ? DateTime.Now : date;
        var prefix = $"VEN-{day:ddMMyyyy}-";
        var existing = await _db.PurchaseInvoices.AsNoTracking()
            .Where(x => x.VendorInvoiceNo != null && x.VendorInvoiceNo.StartsWith(prefix))
            .Select(x => x.VendorInvoiceNo!)
            .ToListAsync(ct);

        var max = 0;
        foreach (var no in existing)
        {
            var suffix = no.Length > prefix.Length ? no[prefix.Length..] : string.Empty;
            if (int.TryParse(suffix, out var n) && n > max)
                max = n;
        }

        return $"{prefix}{max + 1}";
    }

    private void ValidatePurchaseRequiredFields(PurchaseInvoiceFormViewModel model, bool requireReceiveLines)
    {
        if (model.VendorUid <= 0)
            ModelState.AddModelError(nameof(model.VendorUid), "Vendor is required.");

        if (model.BranchUid is null or <= 0)
            ModelState.AddModelError(nameof(model.BranchUid), "Branch is required.");

        if (string.IsNullOrWhiteSpace(model.VendorInvoiceNo))
            ModelState.AddModelError(nameof(model.VendorInvoiceNo), "Vendor Invoice No is required.");

        if (requireReceiveLines)
        {
            var receiveLines = (model.Lines ?? [])
                .Where(x => x.ProductUid is > 0 && x.Quantity > 0 && x.UnitCost >= 0)
                .ToList();
            if (receiveLines.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Lines),
                    "Add at least one item in Receive into inventory (select product, quantity, and unit cost).");
            }
        }
    }

    private static void EnsureLineRows(PurchaseInvoiceFormViewModel model)
    {
        model.Lines ??= [];
        var minRows = Math.Max(15, model.Lines.Count);
        while (model.Lines.Count < minRows)
            model.Lines.Add(new PurchaseInvoiceLineFormViewModel());
    }

    private async Task PopulateExistingPurchaseLinesAsync(
        PurchaseInvoiceFormViewModel form,
        int purchaseInvoiceUid,
        CancellationToken ct)
    {
        var existing = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == purchaseInvoiceUid)
            .OrderBy(x => x.Uid)
            .Select(x => new PurchaseInvoiceLineFormViewModel
            {
                ExistingDetailUid = x.Uid,
                ProductUid = x.ProductUid,
                Quantity = x.Quantity,
                UnitCost = x.UnitCost
            })
            .ToListAsync(ct);

        // Keep any newly typed blank/add rows from a failed POST after the existing ones.
        var newRows = (form.Lines ?? [])
            .Where(x => x.ExistingDetailUid is null)
            .ToList();

        form.Lines = existing;
        form.Lines.AddRange(newRows);
    }

    private async Task LoadVendorPaymentContextAsync(int purchaseInvoiceUid, CancellationToken ct)
    {
        ViewBag.ExistingLines = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == purchaseInvoiceUid)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        ViewBag.VendorPayments = await _db.VendorPayments.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == purchaseInvoiceUid)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.Uid)
            .Select(p => new VendorPaymentListItemViewModel
            {
                Uid = p.Uid,
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                Amount = p.Amount,
                ReferenceNo = p.ReferenceNo
            })
            .ToListAsync(ct);

        var paymentMethods = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.PaymentName)
            .ThenBy(x => x.Uid)
            .Select(x => new { x.Uid, x.PaymentName })
            .ToListAsync(ct);

        ViewBag.PaymentMethods = paymentMethods
            .GroupBy(x => x.PaymentName.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(x => x.PaymentName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new SelectListItem { Value = x.Uid.ToString(), Text = x.PaymentName })
            .ToList();
    }

    private async Task RecalculatePurchaseTotalsAsync(PurchaseInvoice invoice, CancellationToken ct)
    {
        var subTotal = await _db.PurchaseInvoiceDetails.AsNoTracking()
            .Where(x => x.PurchaseInvoiceUid == invoice.Uid)
            .SumAsync(x => (decimal?)x.LineTotal, ct) ?? 0m;

        invoice.SubTotal = subTotal;
        invoice.NetAmount = Math.Max(0, subTotal + invoice.TaxAmount + invoice.OtherCharges - invoice.DiscountAmount);
        if (invoice.PaidAmount < 0) invoice.PaidAmount = 0;
        if (invoice.PaidAmount > invoice.NetAmount) invoice.PaidAmount = invoice.NetAmount;
        invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        invoice.PaymentStatus = invoice.PaidAmount <= 0
            ? "Unpaid"
            : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
    }

    /// <summary>
    /// Fresh Yogurt receipt consumes milk (40 milk : 32 yogurt) at the same warehouse and posts a Shop sales invoice.
    /// </summary>
    private async Task<string?> ApplyYogurtMilkConsumptionAsync(
        IReadOnlyList<InventoryLineMovement> movements,
        int warehouseUid,
        int? branchUid,
        int? userUid,
        int? terminalUid,
        string purchaseInvoiceNo,
        CancellationToken ct)
    {
        var yogurtQty = movements
            .Where(m => YogurtRecipeService.IsFreshYogurtProduct(m.ProductName) && m.Quantity > 0)
            .Sum(m => m.Quantity);
        if (yogurtQty <= 0)
            return null;

        string? branchName = null;
        if (branchUid is > 0)
        {
            branchName = await _db.Branches.AsNoTracking()
                .Where(b => b.Uid == branchUid)
                .Select(b => b.BranchName)
                .FirstOrDefaultAsync(ct);
        }

        string? terminalName = null;
        if (terminalUid is > 0)
        {
            terminalName = await _db.Terminals.AsNoTracking()
                .Where(t => t.Uid == terminalUid)
                .Select(t => t.TerminalName)
                .FirstOrDefaultAsync(ct);
        }

        var result = await _yogurtRecipe.ConsumeMilkForReceivedYogurtAsync(
            yogurtQty,
            warehouseUid,
            branchUid,
            branchName,
            userUid,
            terminalUid,
            terminalName,
            purchaseInvoiceNo,
            ct);

        return result.Message;
    }

    private static PurchaseInvoiceFormViewModel ToForm(PurchaseInvoice e) => new()
    {
        Uid = e.Uid,
        PurchaseInvoiceNo = e.PurchaseInvoiceNo,
        VendorInvoiceNo = e.VendorInvoiceNo ?? string.Empty,
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
        VendorInvoiceNo = string.IsNullOrWhiteSpace(m.VendorInvoiceNo) ? null : m.VendorInvoiceNo.Trim(),
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
        e.VendorInvoiceNo = string.IsNullOrWhiteSpace(m.VendorInvoiceNo) ? null : m.VendorInvoiceNo.Trim();
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
