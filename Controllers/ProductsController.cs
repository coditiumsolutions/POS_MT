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
public class ProductsController : Controller
{
    private static readonly HashSet<string> AllowedImageExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    private const long MaxImageBytes = 2 * 1024 * 1024;
    private const string ProductImageFolder = "uploads/products";

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

    private async Task LoadLookups(CancellationToken ct)
    {
        ViewBag.Categories = new SelectList(
            await _db.ProductCategories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.CategoryName).ToListAsync(ct),
            "Uid", "CategoryName");
        ViewBag.Units = new SelectList(
            await _db.ProductUnits.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.UnitName).ToListAsync(ct),
            "Uid", "UnitName");
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureProductsNav();
        return View(await _db.Products.AsNoTracking().OrderBy(x => x.ProductName).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureProductsNav();
        await LoadLookups(ct);
        return View(new ProductFormViewModel { IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct)
    {
        EnsureProductsNav();
        await ValidateAndSaveImageAsync(model, existingPath: null, ct);
        if (!ModelState.IsValid)
        {
            await LoadLookups(ct);
            return View(model);
        }

        if (await _db.Products.AnyAsync(x => x.ProductCode == model.ProductCode, ct))
        {
            ModelState.AddModelError(nameof(model.ProductCode), "Product code already exists.");
            await LoadLookups(ct);
            return View(model);
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
        await LoadLookups(ct);
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
            await LoadLookups(ct);
            return View(model);
        }

        if (await _db.Products.AnyAsync(x => x.ProductCode == model.ProductCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.ProductCode), "Product code already exists.");
            await LoadLookups(ct);
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

        var inSales = await _db.SalesInvoiceDetails.AsNoTracking().AnyAsync(x => x.ProductUid == id, ct);
        var inPurchases = await _db.PurchaseInvoiceDetails.AsNoTracking().AnyAsync(x => x.ProductUid == id, ct);
        var inAdjustments = await _db.StockAdjustmentDetails.AsNoTracking().AnyAsync(x => x.ProductUid == id, ct);
        var inTransactions = await _db.InventoryTransactions.AsNoTracking().AnyAsync(x => x.ProductUid == id, ct);
        if (inSales || inPurchases || inAdjustments || inTransactions)
        {
            TempData["Error"] = $"Cannot delete product {entity.ProductCode} because it is used in sales, purchases, stock adjustments, or inventory transactions.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var inventoryRows = await _db.Inventories.Where(x => x.ProductUid == id).ToListAsync(ct);
        if (inventoryRows.Count > 0)
        {
            _db.Inventories.RemoveRange(inventoryRows);
        }

        var code = entity.ProductCode;
        var imagePath = entity.ImagePath;
        _db.Products.Remove(entity);
        await _db.SaveChangesAsync(ct);
        TryDeleteProductImage(imagePath);
        await _audit.WriteAsync("Delete", "Products", id.ToString(), $"Deleted product {code}", ct);
        TempData["Success"] = "Product deleted.";
        return RedirectToAction(nameof(Index));
    }

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
