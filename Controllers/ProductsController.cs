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
public class ProductsController : Controller
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxImageBytes = 2 * 1024 * 1024;
    private const string ProductImageFolder = "uploads/products";
    private const string ProductCodePrefix = "PRD-";
    private const int ProductCodeStartNumber = 1000;

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly IWebHostEnvironment _env;

    public ProductsController(
        POSDbContext db,
        IAuditService audit,
        INavContextService navContext,
        IWebHostEnvironment env)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
        _env = env;
    }

    private void EnsureProductsNav() => _navContext.SetArea("Products");

    private void MarkProductCodeReadonly() => ViewBag.ProductCodeReadonly = true;

    private async Task LoadLookups(int? selectedUnitUid = null, CancellationToken ct = default)
    {
        ViewBag.Categories = new SelectList(
            await _db.ProductCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.CategoryName).ToListAsync(ct),
            "Uid", "CategoryName");

        var units = await _db.ProductUnits.AsNoTracking()
            .Where(x => x.IsActive)
            .ToListAsync(ct);

        // ProductUnits has duplicate UnitCode rows; show one option per code.
        var distinctUnits = units
            .GroupBy(x => x.UnitCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(x => x.Uid).First())
            .OrderBy(x => x.UnitName)
            .ToList();

        if (selectedUnitUid is int uid && distinctUnits.All(x => x.Uid != uid))
        {
            var selected = units.FirstOrDefault(x => x.Uid == uid)
                ?? await _db.ProductUnits.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == uid, ct);
            if (selected is not null)
            {
                distinctUnits.Add(selected);
                distinctUnits = distinctUnits.OrderBy(x => x.UnitName).ToList();
            }
        }

        ViewBag.Units = new SelectList(distinctUnits, "Uid", "UnitName", selectedUnitUid);
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureProductsNav();
        return View(await _db.Products.AsNoTracking().OrderBy(x => x.ProductName).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureProductsNav();
        MarkProductCodeReadonly();
        await LoadLookups(ct: ct);
        return View(new ProductFormViewModel
        {
            IsActive = true,
            ProductCode = await GenerateNextProductCodeAsync(ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        EnsureProductsNav();
        MarkProductCodeReadonly();

        model.ProductCode = await GenerateNextProductCodeAsync(ct);
        ModelState.Remove(nameof(model.ProductCode));

        await ValidateAndSaveImageAsync(model, existingPath: null, ct);
        if (!ModelState.IsValid)
        {
            await LoadLookups(model.UnitUid, ct);
            return View(model);
        }

        if (await _db.Products.AnyAsync(x => x.ProductCode == model.ProductCode, ct))
        {
            model.ProductCode = await GenerateNextProductCodeAsync(ct);
            if (await _db.Products.AnyAsync(x => x.ProductCode == model.ProductCode, ct))
            {
                ModelState.AddModelError(nameof(model.ProductCode), "Could not generate a unique product code. Please try again.");
                await LoadLookups(model.UnitUid, ct);
                return View(model);
            }
        }

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;
        _db.Products.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Products", e.Uid.ToString(), $"Created product {e.ProductCode}", ct);
        TempData["Success"] = "Product created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.Products.FindAsync([id], ct);
        if (e is null) return NotFound();
        await LoadLookups(e.UnitUid, ct);
        return View(ToForm(e));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct)
    {
        EnsureProductsNav();
        if (id != model.Uid) return BadRequest();

        var e = await _db.Products.FindAsync([id], ct);
        if (e is null) return NotFound();

        await ValidateAndSaveImageAsync(model, e.ImagePath, ct);
        if (!ModelState.IsValid)
        {
            await LoadLookups(model.UnitUid, ct);
            return View(model);
        }

        if (await _db.Products.AnyAsync(x => x.ProductCode == model.ProductCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.ProductCode), "Product code already exists.");
            await LoadLookups(model.UnitUid, ct);
            return View(model);
        }

        var previousImage = e.ImagePath;
        Apply(e, model);
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        if (!string.Equals(previousImage, e.ImagePath, StringComparison.OrdinalIgnoreCase))
        {
            TryDeleteProductImage(previousImage);
        }

        await _audit.WriteAsync("Update", "Products", e.Uid.ToString(), $"Updated product {e.ProductCode}", ct);
        TempData["Success"] = "Product updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.Products.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var entity = await _db.Products.FindAsync([id], ct);
        if (entity is null) return NotFound();

        var code = entity.ProductCode;
        var imagePath = entity.ImagePath;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var removed = await RemoveProductUsageAsync(id, code, ct);
            _db.Products.Remove(entity);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "Products", id.ToString(),
                $"Deleted product {code} and related records (sales {removed.SalesLines}, purchases {removed.PurchaseLines}, adjustments {removed.AdjustmentLines}, inventory tx {removed.InventoryTransactions}, stock rows {removed.InventoryRows}).", ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Cannot delete product {code} because other records still use it.";
            return RedirectToAction(nameof(Index));
        }

        TryDeleteProductImage(imagePath);
        TempData["Success"] = $"Product {code} and its related records were deleted.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<ProductUsageRemoval> RemoveProductUsageAsync(int productId, string productCode, CancellationToken ct)
    {
        var salesDetails = await _db.SalesInvoiceDetails.Where(x => x.ProductUid == productId).ToListAsync(ct);
        var purchaseDetails = await _db.PurchaseInvoiceDetails.Where(x => x.ProductUid == productId).ToListAsync(ct);
        var adjustmentDetails = await _db.StockAdjustmentDetails.Where(x => x.ProductUid == productId).ToListAsync(ct);
        var transactions = await _db.InventoryTransactions.Where(x => x.ProductUid == productId).ToListAsync(ct);
        var inventoryRows = await _db.Inventories.Where(x => x.ProductUid == productId).ToListAsync(ct);
        var monthly = await _db.CustomerMonthlySupplies
            .Where(x => x.ProductUid == productId || x.ProductCode == productCode)
            .ToListAsync(ct);
        var monthlyOverrides = await _db.CustomerMonthlySupplyOverrides
            .Where(x => x.ProductUid == productId || x.ProductCode == productCode)
            .ToListAsync(ct);

        var salesInvoiceIds = salesDetails.Select(x => x.SalesInvoiceUid).Distinct().ToList();
        var purchaseInvoiceIds = purchaseDetails.Select(x => x.PurchaseInvoiceUid).Distinct().ToList();
        var adjustmentIds = adjustmentDetails.Select(x => x.StockAdjustmentUid).Distinct().ToList();

        if (salesDetails.Count > 0) _db.SalesInvoiceDetails.RemoveRange(salesDetails);
        if (purchaseDetails.Count > 0) _db.PurchaseInvoiceDetails.RemoveRange(purchaseDetails);
        if (adjustmentDetails.Count > 0) _db.StockAdjustmentDetails.RemoveRange(adjustmentDetails);
        if (transactions.Count > 0) _db.InventoryTransactions.RemoveRange(transactions);
        if (inventoryRows.Count > 0) _db.Inventories.RemoveRange(inventoryRows);
        if (monthly.Count > 0) _db.CustomerMonthlySupplies.RemoveRange(monthly);
        if (monthlyOverrides.Count > 0) _db.CustomerMonthlySupplyOverrides.RemoveRange(monthlyOverrides);
        await _db.SaveChangesAsync(ct);

        await RefreshSalesInvoicesAsync(salesInvoiceIds, ct);
        await RefreshPurchaseInvoicesAsync(purchaseInvoiceIds, ct);
        await RemoveEmptyAdjustmentsAsync(adjustmentIds, ct);

        return new ProductUsageRemoval(
            salesDetails.Count,
            purchaseDetails.Count,
            adjustmentDetails.Count,
            transactions.Count,
            inventoryRows.Count);
    }

    private async Task RefreshSalesInvoicesAsync(List<int> invoiceIds, CancellationToken ct)
    {
        if (invoiceIds.Count == 0) return;

        var invoices = await _db.SalesInvoices.Where(x => invoiceIds.Contains(x.Uid)).ToListAsync(ct);
        var remaining = await _db.SalesInvoiceDetails
            .Where(x => invoiceIds.Contains(x.SalesInvoiceUid))
            .ToListAsync(ct);
        var linesByInvoice = remaining.GroupBy(x => x.SalesInvoiceUid).ToDictionary(g => g.Key, g => g.ToList());
        var emptyIds = new List<int>();

        foreach (var invoice in invoices)
        {
            if (!linesByInvoice.TryGetValue(invoice.Uid, out var lines) || lines.Count == 0)
            {
                emptyIds.Add(invoice.Uid);
                continue;
            }

            ApplyInvoiceTotals(
                lines.Sum(x => x.LineTotal),
                lines.Sum(x => x.DiscountAmount),
                lines.Sum(x => x.TaxAmount),
                invoice.OtherCharges,
                invoice.PaidAmount,
                (sub, discount, tax, net, balance, status) =>
                {
                    invoice.SubTotal = sub;
                    invoice.DiscountAmount = discount;
                    invoice.TaxAmount = tax;
                    invoice.NetAmount = net;
                    invoice.BalanceAmount = balance;
                    invoice.PaymentStatus = status;
                    invoice.UpdatedDate = DateTime.Now;
                });
        }

        if (emptyIds.Count > 0)
        {
            var payments = await _db.CustomerPayments
                .Where(x => x.SalesInvoiceUid != null && emptyIds.Contains(x.SalesInvoiceUid.Value))
                .ToListAsync(ct);
            foreach (var payment in payments)
                payment.SalesInvoiceUid = null;

            _db.SalesInvoices.RemoveRange(invoices.Where(x => emptyIds.Contains(x.Uid)));
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RefreshPurchaseInvoicesAsync(List<int> invoiceIds, CancellationToken ct)
    {
        if (invoiceIds.Count == 0) return;

        var invoices = await _db.PurchaseInvoices.Where(x => invoiceIds.Contains(x.Uid)).ToListAsync(ct);
        var remaining = await _db.PurchaseInvoiceDetails
            .Where(x => invoiceIds.Contains(x.PurchaseInvoiceUid))
            .ToListAsync(ct);
        var linesByInvoice = remaining.GroupBy(x => x.PurchaseInvoiceUid).ToDictionary(g => g.Key, g => g.ToList());
        var emptyIds = new List<int>();

        foreach (var invoice in invoices)
        {
            if (!linesByInvoice.TryGetValue(invoice.Uid, out var lines) || lines.Count == 0)
            {
                emptyIds.Add(invoice.Uid);
                continue;
            }

            ApplyInvoiceTotals(
                lines.Sum(x => x.LineTotal),
                lines.Sum(x => x.DiscountAmount),
                lines.Sum(x => x.TaxAmount),
                invoice.OtherCharges,
                invoice.PaidAmount,
                (sub, discount, tax, net, balance, status) =>
                {
                    invoice.SubTotal = sub;
                    invoice.DiscountAmount = discount;
                    invoice.TaxAmount = tax;
                    invoice.NetAmount = net;
                    invoice.BalanceAmount = balance;
                    invoice.PaymentStatus = status;
                    invoice.UpdatedDate = DateTime.Now;
                });
        }

        if (emptyIds.Count > 0)
        {
            var payments = await _db.VendorPayments
                .Where(x => x.PurchaseInvoiceUid != null && emptyIds.Contains(x.PurchaseInvoiceUid.Value))
                .ToListAsync(ct);
            foreach (var payment in payments)
                payment.PurchaseInvoiceUid = null;

            _db.PurchaseInvoices.RemoveRange(invoices.Where(x => emptyIds.Contains(x.Uid)));
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task RemoveEmptyAdjustmentsAsync(List<int> adjustmentIds, CancellationToken ct)
    {
        if (adjustmentIds.Count == 0) return;

        var stillUsed = await _db.StockAdjustmentDetails.AsNoTracking()
            .Where(x => adjustmentIds.Contains(x.StockAdjustmentUid))
            .Select(x => x.StockAdjustmentUid)
            .Distinct()
            .ToListAsync(ct);
        var emptyIds = adjustmentIds.Except(stillUsed).ToList();
        if (emptyIds.Count == 0) return;

        var empty = await _db.StockAdjustments.Where(x => emptyIds.Contains(x.Uid)).ToListAsync(ct);
        if (empty.Count > 0)
        {
            _db.StockAdjustments.RemoveRange(empty);
            await _db.SaveChangesAsync(ct);
        }
    }

    private static void ApplyInvoiceTotals(
        decimal lineTotal,
        decimal lineDiscount,
        decimal lineTax,
        decimal otherCharges,
        decimal paidAmount,
        Action<decimal, decimal, decimal, decimal, decimal, string> apply)
    {
        var sub = Math.Round(lineTotal, 2);
        var discount = Math.Round(lineDiscount, 2);
        var tax = Math.Round(lineTax, 2);
        var net = Math.Round(sub - discount + tax + otherCharges, 2);
        if (net < 0) net = 0;
        var balance = Math.Round(net - paidAmount, 2);
        var status = paidAmount <= 0 ? "Unpaid" : paidAmount >= net ? "Paid" : "Partial";
        apply(sub, discount, tax, net, balance, status);
    }

    private readonly record struct ProductUsageRemoval(
        int SalesLines,
        int PurchaseLines,
        int AdjustmentLines,
        int InventoryTransactions,
        int InventoryRows);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.Products.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.IsActive = false;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "Products", e.Uid.ToString(), $"Deactivated {e.ProductCode}", ct);
        TempData["Success"] = "Product deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateNextProductCodeAsync(CancellationToken ct)
    {
        var existing = await _db.Products.AsNoTracking()
            .Where(x => x.ProductCode.StartsWith(ProductCodePrefix))
            .Select(x => x.ProductCode)
            .ToListAsync(ct);

        var maxNumber = ProductCodeStartNumber - 1;
        foreach (var code in existing)
        {
            var suffix = code.Length > ProductCodePrefix.Length
                ? code[ProductCodePrefix.Length..]
                : string.Empty;
            if (int.TryParse(suffix, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{ProductCodePrefix}{maxNumber + 1}";
    }

    private async Task ValidateAndSaveImageAsync(ProductFormViewModel model, string? existingPath, CancellationToken ct)
    {
        if (model.ImageFile is null || model.ImageFile.Length == 0)
        {
            if (string.IsNullOrWhiteSpace(model.ImagePath))
            {
                model.ImagePath = existingPath;
            }
            return;
        }

        var ext = Path.GetExtension(model.ImageFile.FileName);
        if (string.IsNullOrWhiteSpace(ext) || !AllowedImageExtensions.Contains(ext))
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Only JPG, PNG, GIF, or WEBP images are allowed.");
            return;
        }

        if (model.ImageFile.Length > MaxImageBytes)
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Image must be 2 MB or smaller.");
            return;
        }

        var contentType = model.ImageFile.ContentType ?? string.Empty;
        if (!contentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError(nameof(model.ImageFile), "Uploaded file must be an image.");
            return;
        }

        var folder = Path.Combine(_env.WebRootPath, "uploads", "products");
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
        var physicalPath = Path.Combine(folder, fileName);
        await using (var stream = System.IO.File.Create(physicalPath))
        {
            await model.ImageFile.CopyToAsync(stream, ct);
        }

        model.ImagePath = "/" + ProductImageFolder + "/" + fileName;
    }

    private void TryDeleteProductImage(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var normalized = relativePath.Replace('\\', '/').TrimStart('/');
        if (!normalized.StartsWith(ProductImageFolder + "/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var physical = Path.Combine(_env.WebRootPath, normalized.Replace('/', Path.DirectorySeparatorChar));
        if (System.IO.File.Exists(physical))
        {
            try { System.IO.File.Delete(physical); }
            catch { /* ignore cleanup failures */ }
        }
    }

    private static ProductFormViewModel ToForm(Product e) => new()
    {
        Uid = e.Uid,
        ProductCode = e.ProductCode,
        Barcode = e.Barcode,
        ProductName = e.ProductName,
        CategoryUid = e.CategoryUid,
        UnitUid = e.UnitUid,
        BrandName = e.BrandName,
        PurchasePrice = e.PurchasePrice,
        SalePrice = e.SalePrice,
        WholesalePrice = e.WholesalePrice,
        MinimumStock = e.MinimumStock,
        MaximumStock = e.MaximumStock,
        TaxPercent = e.TaxPercent,
        DiscountPercent = e.DiscountPercent,
        IsTaxable = e.IsTaxable,
        IsActive = e.IsActive,
        Description = e.Description,
        ImagePath = e.ImagePath
    };

    private static Product ToEntity(ProductFormViewModel m) => new()
    {
        ProductCode = m.ProductCode.Trim(),
        Barcode = m.Barcode,
        ProductName = m.ProductName.Trim(),
        CategoryUid = m.CategoryUid,
        UnitUid = m.UnitUid,
        BrandName = m.BrandName,
        PurchasePrice = m.PurchasePrice,
        SalePrice = m.SalePrice,
        WholesalePrice = m.WholesalePrice,
        MinimumStock = m.MinimumStock,
        MaximumStock = m.MaximumStock,
        TaxPercent = m.TaxPercent,
        DiscountPercent = m.DiscountPercent,
        IsTaxable = m.IsTaxable,
        IsActive = m.IsActive,
        Description = m.Description,
        ImagePath = m.ImagePath
    };

    private static void Apply(Product e, ProductFormViewModel m)
    {
        e.ProductCode = m.ProductCode.Trim();
        e.Barcode = m.Barcode;
        e.ProductName = m.ProductName.Trim();
        e.CategoryUid = m.CategoryUid;
        e.UnitUid = m.UnitUid;
        e.BrandName = m.BrandName;
        e.PurchasePrice = m.PurchasePrice;
        e.SalePrice = m.SalePrice;
        e.WholesalePrice = m.WholesalePrice;
        e.MinimumStock = m.MinimumStock;
        e.MaximumStock = m.MaximumStock;
        e.TaxPercent = m.TaxPercent;
        e.DiscountPercent = m.DiscountPercent;
        e.IsTaxable = m.IsTaxable;
        e.IsActive = m.IsActive;
        e.Description = m.Description;
        e.ImagePath = m.ImagePath;
    }
}
