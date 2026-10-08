using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class ProductsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit; private readonly INavContextService _navContext;
  public ProductsController(POSDbContext db, IAuditService audit, INavContextService navContext){ _db=db; _audit=audit; _navContext=navContext; }
  private void EnsureProductsNav() => _navContext.SetArea("Products");
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Categories = new SelectList(await _db.ProductCategories.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CategoryName).ToListAsync(ct), "Uid", "CategoryName");
    ViewBag.Units = new SelectList(await _db.ProductUnits.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.UnitName).ToListAsync(ct), "Uid", "UnitName");
  }
  public async Task<IActionResult> Index(CancellationToken ct){ EnsureProductsNav(); return View(await _db.Products.AsNoTracking().OrderBy(x=>x.ProductName).ToListAsync(ct)); }
  public async Task<IActionResult> Create(CancellationToken ct){ EnsureProductsNav(); await LoadLookups(ct); return View(new ProductFormViewModel{IsActive=true}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(ProductFormViewModel model, CancellationToken ct){
    EnsureProductsNav();
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    if(await _db.Products.AnyAsync(x=>x.ProductCode==model.ProductCode,ct)){ ModelState.AddModelError(nameof(model.ProductCode),"Product code already exists."); await LoadLookups(ct); return View(model);} 
    var e=ToEntity(model); e.CreatedDate=DateTime.Now; _db.Products.Add(e); await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Create","Products",e.Uid.ToString(),$"Created product {e.ProductCode}",ct);
    TempData["Success"]="Product created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ EnsureProductsNav(); var e=await _db.Products.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(ToForm(e)); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, ProductFormViewModel model, CancellationToken ct){
    EnsureProductsNav();
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.Products.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.Products.AnyAsync(x=>x.ProductCode==model.ProductCode && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.ProductCode),"Product code already exists."); await LoadLookups(ct); return View(model);} 
    Apply(e,model); e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Update","Products",e.Uid.ToString(),$"Updated product {e.ProductCode}",ct);
    TempData["Success"]="Product updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ EnsureProductsNav(); var e=await _db.Products.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ EnsureProductsNav(); var e=await _db.Products.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","Products",e.Uid.ToString(),$"Deactivated {e.ProductCode}",ct); TempData["Success"]="Product deactivated."; return RedirectToAction(nameof(Index)); }
  private static ProductFormViewModel ToForm(Product e)=> new(){Uid=e.Uid,ProductCode=e.ProductCode,Barcode=e.Barcode,ProductName=e.ProductName,CategoryUid=e.CategoryUid,UnitUid=e.UnitUid,BrandName=e.BrandName,PurchasePrice=e.PurchasePrice,SalePrice=e.SalePrice,WholesalePrice=e.WholesalePrice,MinimumStock=e.MinimumStock,MaximumStock=e.MaximumStock,TaxPercent=e.TaxPercent,DiscountPercent=e.DiscountPercent,IsTaxable=e.IsTaxable,IsActive=e.IsActive,Description=e.Description};
  private static Product ToEntity(ProductFormViewModel m)=> new(){ProductCode=m.ProductCode.Trim(),Barcode=m.Barcode,ProductName=m.ProductName.Trim(),CategoryUid=m.CategoryUid,UnitUid=m.UnitUid,BrandName=m.BrandName,PurchasePrice=m.PurchasePrice,SalePrice=m.SalePrice,WholesalePrice=m.WholesalePrice,MinimumStock=m.MinimumStock,MaximumStock=m.MaximumStock,TaxPercent=m.TaxPercent,DiscountPercent=m.DiscountPercent,IsTaxable=m.IsTaxable,IsActive=m.IsActive,Description=m.Description};
  private static void Apply(Product e, ProductFormViewModel m){ e.ProductCode=m.ProductCode.Trim(); e.Barcode=m.Barcode; e.ProductName=m.ProductName.Trim(); e.CategoryUid=m.CategoryUid; e.UnitUid=m.UnitUid; e.BrandName=m.BrandName; e.PurchasePrice=m.PurchasePrice; e.SalePrice=m.SalePrice; e.WholesalePrice=m.WholesalePrice; e.MinimumStock=m.MinimumStock; e.MaximumStock=m.MaximumStock; e.TaxPercent=m.TaxPercent; e.DiscountPercent=m.DiscountPercent; e.IsTaxable=m.IsTaxable; e.IsActive=m.IsActive; e.Description=m.Description; }
}
