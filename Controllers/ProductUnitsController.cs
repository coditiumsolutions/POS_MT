using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class ProductUnitsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public ProductUnitsController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.ProductUnits.AsNoTracking().OrderBy(x=>x.UnitName).ToListAsync(ct));
  public IActionResult Create()=>View(new ProductUnitFormViewModel{IsActive=true});
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(ProductUnitFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid) return View(model);
    if(await _db.ProductUnits.AnyAsync(x=>x.UnitCode==model.UnitCode,ct)){ ModelState.AddModelError(nameof(model.UnitCode),"Unit code already exists."); return View(model);} 
    var e=new ProductUnit{UnitCode=model.UnitCode.Trim(),UnitName=model.UnitName.Trim(),DecimalAllowed=model.DecimalAllowed,IsActive=model.IsActive};
    _db.ProductUnits.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","ProductUnits",e.Uid.ToString(),$"Created unit {e.UnitCode}",ct);
    TempData["Success"]="Unit created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.ProductUnits.FindAsync([id],ct); return e is null?NotFound():View(new ProductUnitFormViewModel{Uid=e.Uid,UnitCode=e.UnitCode,UnitName=e.UnitName,DecimalAllowed=e.DecimalAllowed,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, ProductUnitFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid) return View(model);
    var e=await _db.ProductUnits.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.ProductUnits.AnyAsync(x=>x.UnitCode==model.UnitCode && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.UnitCode),"Unit code already exists."); return View(model);} 
    e.UnitCode=model.UnitCode.Trim(); e.UnitName=model.UnitName.Trim(); e.DecimalAllowed=model.DecimalAllowed; e.IsActive=model.IsActive;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","ProductUnits",e.Uid.ToString(),$"Updated unit {e.UnitCode}",ct);
    TempData["Success"]="Unit updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.ProductUnits.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.ProductUnits.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","ProductUnits",e.Uid.ToString(),$"Deactivated {e.UnitCode}",ct); TempData["Success"]="Unit deactivated."; return RedirectToAction(nameof(Index)); }
}
