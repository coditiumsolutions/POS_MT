using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class UsersController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit; private readonly IPasswordHashService _hash;
  public UsersController(POSDbContext db, IAuditService audit, IPasswordHashService hash){ _db=db; _audit=audit; _hash=hash; }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.Users.AsNoTracking().OrderBy(x=>x.UserName).ToListAsync(ct));
  public IActionResult Create()=>View(new UserFormViewModel{IsActive=true, RoleName="Cashier"});
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(UserFormViewModel model, CancellationToken ct){
    if(string.IsNullOrWhiteSpace(model.Password)) ModelState.AddModelError(nameof(model.Password), "Password is required.");
    if(!ModelState.IsValid) return View(model);
    if(await _db.Users.AnyAsync(x=>x.UserName==model.UserName,ct)){ ModelState.AddModelError(nameof(model.UserName),"Username already exists."); return View(model);} 
    var e=new User{UserName=model.UserName.Trim(),FullName=model.FullName.Trim(),PasswordHash=_hash.Hash(model.Password!),MobileNo=model.MobileNo,Email=model.Email,RoleName=model.RoleName.Trim(),UserType=model.UserType,IsActive=model.IsActive,CreatedDate=DateTime.Now};
    _db.Users.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Users",e.Uid.ToString(),$"Created user {e.UserName}",ct);
    TempData["Success"]="User created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Users.FindAsync([id],ct); return e is null?NotFound():View(new UserFormViewModel{Uid=e.Uid,UserName=e.UserName,FullName=e.FullName,MobileNo=e.MobileNo,Email=e.Email,RoleName=e.RoleName,UserType=e.UserType,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, UserFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid) return View(model);
    var e=await _db.Users.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.Users.AnyAsync(x=>x.UserName==model.UserName && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.UserName),"Username already exists."); return View(model);} 
    e.UserName=model.UserName.Trim(); e.FullName=model.FullName.Trim(); e.MobileNo=model.MobileNo; e.Email=model.Email; e.RoleName=model.RoleName.Trim(); e.UserType=model.UserType; e.IsActive=model.IsActive; e.UpdatedDate=DateTime.Now;
    if(!string.IsNullOrWhiteSpace(model.Password)) e.PasswordHash=_hash.Hash(model.Password);
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Users",e.Uid.ToString(),$"Updated user {e.UserName}",ct);
    TempData["Success"]="User updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Users.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.Users.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","Users",e.Uid.ToString(),$"Deactivated {e.UserName}",ct); TempData["Success"]="User deactivated."; return RedirectToAction(nameof(Index)); }
}
