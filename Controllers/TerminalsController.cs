using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class TerminalsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public TerminalsController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadBranches(CancellationToken ct)=> ViewBag.Branches = new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct), "Uid", "BranchName");
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.Terminals.AsNoTracking().OrderBy(x=>x.TerminalName).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadBranches(ct); return View(new TerminalFormViewModel{IsActive=true}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(TerminalFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadBranches(ct); return View(model);} 
    if(await _db.Terminals.AnyAsync(x=>x.TerminalCode==model.TerminalCode,ct)){ ModelState.AddModelError(nameof(model.TerminalCode),"Terminal code already exists."); await LoadBranches(ct); return View(model);} 
    var e=new Terminal{TerminalCode=model.TerminalCode.Trim(),TerminalName=model.TerminalName.Trim(),BranchUid=model.BranchUid,ComputerName=model.ComputerName,Ipaddress=model.Ipaddress,IsActive=model.IsActive,CreatedDate=DateTime.Now};
    _db.Terminals.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Terminals",e.Uid.ToString(),$"Created terminal {e.TerminalCode}",ct);
    TempData["Success"]="Terminal created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Terminals.FindAsync([id],ct); if(e is null) return NotFound(); await LoadBranches(ct); return View(new TerminalFormViewModel{Uid=e.Uid,TerminalCode=e.TerminalCode,TerminalName=e.TerminalName,BranchUid=e.BranchUid,ComputerName=e.ComputerName,Ipaddress=e.Ipaddress,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, TerminalFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadBranches(ct); return View(model);} 
    var e=await _db.Terminals.FindAsync([id],ct); if(e is null) return NotFound();
    if(await _db.Terminals.AnyAsync(x=>x.TerminalCode==model.TerminalCode && x.Uid!=id,ct)){ ModelState.AddModelError(nameof(model.TerminalCode),"Terminal code already exists."); await LoadBranches(ct); return View(model);} 
    e.TerminalCode=model.TerminalCode.Trim(); e.TerminalName=model.TerminalName.Trim(); e.BranchUid=model.BranchUid; e.ComputerName=model.ComputerName; e.Ipaddress=model.Ipaddress; e.IsActive=model.IsActive; e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Terminals",e.Uid.ToString(),$"Updated terminal {e.TerminalCode}",ct);
    TempData["Success"]="Terminal updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Terminals.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.Terminals.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","Terminals",e.Uid.ToString(),$"Deactivated {e.TerminalCode}",ct); TempData["Success"]="Terminal deactivated."; return RedirectToAction(nameof(Index)); }
}
