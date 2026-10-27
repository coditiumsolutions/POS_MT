using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class ProductCategoriesController : Controller
{
    private const string CategoryCodePrefix = "CAT-";
    private const int CategoryCodeStartNumber = 1000;

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;

    public ProductCategoriesController(POSDbContext db, IAuditService audit, INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
    }

    private void EnsureProductsNav() => _navContext.SetArea("Products");

    private void MarkCategoryCodeReadonly() => ViewBag.CategoryCodeReadonly = true;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureProductsNav();
        return View(await _db.ProductCategories.AsNoTracking().OrderBy(x => x.CategoryName).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureProductsNav();
        MarkCategoryCodeReadonly();
        return View(new ProductCategoryFormViewModel
        {
            IsActive = true,
            CategoryCode = await GenerateNextCategoryCodeAsync(ct)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ProductCategoryFormViewModel model, CancellationToken ct)
    {
        EnsureProductsNav();
        MarkCategoryCodeReadonly();

        model.CategoryCode = await GenerateNextCategoryCodeAsync(ct);
        ModelState.Remove(nameof(model.CategoryCode));

        if (!ModelState.IsValid)
            return View(model);

        if (await _db.ProductCategories.AnyAsync(x => x.CategoryCode == model.CategoryCode, ct))
        {
            model.CategoryCode = await GenerateNextCategoryCodeAsync(ct);
            if (await _db.ProductCategories.AnyAsync(x => x.CategoryCode == model.CategoryCode, ct))
            {
                ModelState.AddModelError(nameof(model.CategoryCode), "Could not generate a unique category code. Please try again.");
                return View(model);
            }
        }

        var e = new ProductCategory
        {
            CategoryCode = model.CategoryCode.Trim(),
            CategoryName = model.CategoryName.Trim(),
            Description = model.Description,
            IsActive = model.IsActive,
            CreatedDate = DateTime.Now
        };
        _db.ProductCategories.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "ProductCategories", e.Uid.ToString(), $"Created category {e.CategoryCode}", ct);
        TempData["Success"] = "Category created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.ProductCategories.FindAsync([id], ct);
        if (e is null) return NotFound();
        return View(new ProductCategoryFormViewModel
        {
            Uid = e.Uid,
            CategoryCode = e.CategoryCode,
            CategoryName = e.CategoryName,
            Description = e.Description,
            IsActive = e.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ProductCategoryFormViewModel model, CancellationToken ct)
    {
        EnsureProductsNav();
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid)
            return View(model);

        var e = await _db.ProductCategories.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (await _db.ProductCategories.AnyAsync(x => x.CategoryCode == model.CategoryCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.CategoryCode), "Category code already exists.");
            return View(model);
        }

        e.CategoryCode = model.CategoryCode.Trim();
        e.CategoryName = model.CategoryName.Trim();
        e.Description = model.Description;
        e.IsActive = model.IsActive;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "ProductCategories", e.Uid.ToString(), $"Updated category {e.CategoryCode}", ct);
        TempData["Success"] = "Category updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.ProductCategories.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.ProductCategories.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.IsActive = false;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "ProductCategories", e.Uid.ToString(), $"Deactivated {e.CategoryCode}", ct);
        TempData["Success"] = "Category deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        EnsureProductsNav();
        var e = await _db.ProductCategories.FindAsync([id], ct);
        if (e is null)
        {
            TempData["Error"] = "Category was not found.";
            return RedirectToAction(nameof(Index));
        }

        var code = e.CategoryCode;
        var productCount = await _db.Products.CountAsync(x => x.CategoryUid == id, ct);
        if (productCount > 0)
        {
            TempData["Error"] = $"Cannot delete category {code} because {productCount} product(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        var childCount = await _db.ProductCategories.CountAsync(x => x.ParentCategoryUid == id, ct);
        if (childCount > 0)
        {
            TempData["Error"] = $"Cannot delete category {code} because it has {childCount} child categor{(childCount == 1 ? "y" : "ies")}.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.ProductCategories.Remove(e);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "ProductCategories", id.ToString(), $"Deleted category {code}", ct);
            TempData["Success"] = $"Category {code} deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = $"Cannot delete category {code} because other records still use it.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateNextCategoryCodeAsync(CancellationToken ct)
    {
        var existing = await _db.ProductCategories.AsNoTracking()
            .Where(x => x.CategoryCode.StartsWith(CategoryCodePrefix))
            .Select(x => x.CategoryCode)
            .ToListAsync(ct);

        var maxNumber = CategoryCodeStartNumber - 1;
        foreach (var code in existing)
        {
            var suffix = code.Length > CategoryCodePrefix.Length
                ? code[CategoryCodePrefix.Length..]
                : string.Empty;
            if (int.TryParse(suffix, out var number) && number > maxNumber)
                maxNumber = number;
        }

        return $"{CategoryCodePrefix}{maxNumber + 1}";
    }
}
