using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class BranchesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public BranchesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.Branches.AsNoTracking().OrderBy(x=>x.BranchName).ToListAsync(ct));
  public IActionResult Create()=>View(new BranchFormViewModel{IsActive=true});
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(BranchFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid) return View(model);
    if(await _db.Branches.AnyAsync(x=>x.BranchCode==model.BranchCode,ct)){ ModelState.AddModelError(nameof(model.BranchCode),"Branch code already exists."); return View(model);} 
    var e=new Branch{BranchCode=model.BranchCode.Trim(),BranchName=model.BranchName.Trim(),Address=model.Address,City=model.City,ContactPerson=model.ContactPerson,MobileNo=model.MobileNo,PhoneNo=model.PhoneNo,Email=model.Email,IsActive=model.IsActive,CreatedDate=DateTime.Now};
    _db.Branches.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Branches",e.Uid.ToString(),$"Created branch {e.BranchCode}",ct);
    TempData["Success"]="Branch created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Branches.FindAsync([id],ct); return e is null?NotFound():View(new BranchFormViewModel{Uid=e.Uid,BranchCode=e.BranchCode,BranchName=e.BranchName,Address=e.Address,City=e.City,ContactPerson=e.ContactPerson,MobileNo=e.MobileNo,PhoneNo=e.PhoneNo,Email=e.Email,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, BranchFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid) return View(model);
    var e=await _db.Branches.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.Branches.AnyAsync(x=>x.BranchCode==model.BranchCode && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.BranchCode),"Branch code already exists."); return View(model);} 
    e.BranchCode=model.BranchCode.Trim(); e.BranchName=model.BranchName.Trim(); e.Address=model.Address; e.City=model.City; e.ContactPerson=model.ContactPerson; e.MobileNo=model.MobileNo; e.PhoneNo=model.PhoneNo; e.Email=model.Email; e.IsActive=model.IsActive; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Branches",e.Uid.ToString(),$"Updated branch {e.BranchCode}",ct);
    TempData["Success"]="Branch updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Branches.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.Branches.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","Branches",e.Uid.ToString(),$"Deactivated {e.BranchCode}",ct); TempData["Success"]="Branch deactivated."; return RedirectToAction(nameof(Index)); }
}
