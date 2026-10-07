using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class ProductCategoriesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public ProductCategoriesController(POSDbContext db, IAuditService audit) { _db = db; _audit = audit; }
  private async Task LoadParents(int? excludeId = null, CancellationToken ct = default) {
    var q = _db.ProductCategories.AsNoTracking().Where(x => x.IsActive);
    if (excludeId.HasValue) q = q.Where(x => x.Uid != excludeId.Value);
    ViewBag.Parents = new SelectList(await q.OrderBy(x => x.CategoryName).ToListAsync(ct), "Uid", "CategoryName");
  }
  public async Task<IActionResult> Index(CancellationToken ct) => View(await _db.ProductCategories.AsNoTracking().OrderBy(x => x.CategoryName).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct) { await LoadParents(ct: ct); return View(new ProductCategoryFormViewModel { IsActive = true }); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(ProductCategoryFormViewModel model, CancellationToken ct) {
    if (!ModelState.IsValid) { await LoadParents(ct: ct); return View(model); }
    if (await _db.ProductCategories.AnyAsync(x => x.CategoryCode == model.CategoryCode, ct)) { ModelState.AddModelError(nameof(model.CategoryCode), "Category code already exists."); await LoadParents(ct: ct); return View(model); }
    var e = new ProductCategory { CategoryCode = model.CategoryCode.Trim(), CategoryName = model.CategoryName.Trim(), ParentCategoryUid = model.ParentCategoryUid, Description = model.Description, IsActive = model.IsActive, CreatedDate = DateTime.Now };
    _db.ProductCategories.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","ProductCategories", e.Uid.ToString(), $"Created category {e.CategoryCode}", ct);
    TempData["Success"] = "Category created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct) {
    var e = await _db.ProductCategories.FindAsync([id], ct); if (e is null) return NotFound();
    await LoadParents(id, ct); return View(new ProductCategoryFormViewModel { Uid=e.Uid, CategoryCode=e.CategoryCode, CategoryName=e.CategoryName, ParentCategoryUid=e.ParentCategoryUid, Description=e.Description, IsActive=e.IsActive });
  }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, ProductCategoryFormViewModel model, CancellationToken ct) {
    if (id != model.Uid) return BadRequest();
    if (!ModelState.IsValid) { await LoadParents(id, ct); return View(model); }
    var e = await _db.ProductCategories.FindAsync([id], ct); if (e is null) return NotFound();
    if (await _db.ProductCategories.AnyAsync(x => x.CategoryCode == model.CategoryCode && x.Uid != id, ct)) { ModelState.AddModelError(nameof(model.CategoryCode), "Category code already exists."); await LoadParents(id, ct); return View(model); }
    e.CategoryCode=model.CategoryCode.Trim(); e.CategoryName=model.CategoryName.Trim(); e.ParentCategoryUid=model.ParentCategoryUid; e.Description=model.Description; e.IsActive=model.IsActive; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","ProductCategories", e.Uid.ToString(), $"Updated category {e.CategoryCode}", ct);
    TempData["Success"]="Category updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.ProductCategories.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.ProductCategories.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","ProductCategories",e.Uid.ToString(),$"Deactivated {e.CategoryCode}",ct); TempData["Success"]="Category deactivated."; return RedirectToAction(nameof(Index)); }
}
