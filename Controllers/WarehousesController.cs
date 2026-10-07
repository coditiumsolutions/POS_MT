using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class WarehousesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public WarehousesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadBranches(CancellationToken ct)=> ViewBag.Branches = new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct), "Uid", "BranchName");
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.Warehouses.AsNoTracking().OrderBy(x=>x.WarehouseName).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadBranches(ct); return View(new WarehouseFormViewModel{IsActive=true}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(WarehouseFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadBranches(ct); return View(model);} 
    if(await _db.Warehouses.AnyAsync(x=>x.WarehouseCode==model.WarehouseCode,ct)){ ModelState.AddModelError(nameof(model.WarehouseCode),"Warehouse code already exists."); await LoadBranches(ct); return View(model);} 
    var e=new Warehouse{WarehouseCode=model.WarehouseCode.Trim(),WarehouseName=model.WarehouseName.Trim(),BranchUid=model.BranchUid,Address=model.Address,City=model.City,ContactPerson=model.ContactPerson,MobileNo=model.MobileNo,IsActive=model.IsActive,CreatedDate=DateTime.Now};
    _db.Warehouses.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Warehouses",e.Uid.ToString(),$"Created warehouse {e.WarehouseCode}",ct);
    TempData["Success"]="Warehouse created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Warehouses.FindAsync([id],ct); if(e is null) return NotFound(); await LoadBranches(ct); return View(new WarehouseFormViewModel{Uid=e.Uid,WarehouseCode=e.WarehouseCode,WarehouseName=e.WarehouseName,BranchUid=e.BranchUid,Address=e.Address,City=e.City,ContactPerson=e.ContactPerson,MobileNo=e.MobileNo,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, WarehouseFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadBranches(ct); return View(model);} 
    var e=await _db.Warehouses.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.Warehouses.AnyAsync(x=>x.WarehouseCode==model.WarehouseCode && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.WarehouseCode),"Warehouse code already exists."); await LoadBranches(ct); return View(model);} 
    e.WarehouseCode=model.WarehouseCode.Trim(); e.WarehouseName=model.WarehouseName.Trim(); e.BranchUid=model.BranchUid; e.Address=model.Address; e.City=model.City; e.ContactPerson=model.ContactPerson; e.MobileNo=model.MobileNo; e.IsActive=model.IsActive; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Warehouses",e.Uid.ToString(),$"Updated warehouse {e.WarehouseCode}",ct);
    TempData["Success"]="Warehouse updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Warehouses.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.Warehouses.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","Warehouses",e.Uid.ToString(),$"Deactivated {e.WarehouseCode}",ct); TempData["Success"]="Warehouse deactivated."; return RedirectToAction(nameof(Index)); }
}
