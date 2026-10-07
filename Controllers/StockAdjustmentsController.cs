using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class StockAdjustmentsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public StockAdjustmentsController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Branches=new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct),"Uid","BranchName");
    ViewBag.Warehouses=new SelectList(await _db.Warehouses.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.WarehouseName).ToListAsync(ct),"Uid","WarehouseName");
  }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.StockAdjustments.AsNoTracking().OrderByDescending(x=>x.AdjustmentDate).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new StockAdjustmentFormViewModel{AdjustmentDate=DateTime.Now, AdjustmentNo=$"SA-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(StockAdjustmentFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=new StockAdjustment{AdjustmentNo=model.AdjustmentNo.Trim(),AdjustmentDate=model.AdjustmentDate,BranchUid=model.BranchUid,WarehouseUid=model.WarehouseUid,AdjustmentType=model.AdjustmentType,Remarks=model.Remarks,Status=model.Status,CreatedDate=DateTime.Now};
    _db.StockAdjustments.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","StockAdjustments",e.Uid.ToString(),$"Created adjustment {e.AdjustmentNo}",ct);
    TempData["Success"]="Stock adjustment created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.StockAdjustments.FindAsync([id],ct); if(e is null) return NotFound(); if(e.Status=="Cancelled"){ TempData["Error"]="Cancelled adjustments cannot be edited."; return RedirectToAction(nameof(Index)); } await LoadLookups(ct); return View(new StockAdjustmentFormViewModel{Uid=e.Uid,AdjustmentNo=e.AdjustmentNo,AdjustmentDate=e.AdjustmentDate,BranchUid=e.BranchUid,WarehouseUid=e.WarehouseUid,AdjustmentType=e.AdjustmentType,Remarks=e.Remarks,Status=e.Status}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, StockAdjustmentFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.StockAdjustments.FindAsync([id],ct); if(e is null) return NotFound();
    e.AdjustmentNo=model.AdjustmentNo.Trim(); e.AdjustmentDate=model.AdjustmentDate; e.BranchUid=model.BranchUid; e.WarehouseUid=model.WarehouseUid; e.AdjustmentType=model.AdjustmentType; e.Remarks=model.Remarks; e.Status=model.Status;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","StockAdjustments",e.Uid.ToString(),$"Updated adjustment {e.AdjustmentNo}",ct);
    TempData["Success"]="Stock adjustment updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.StockAdjustments.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Cancel(int id, CancellationToken ct){ var e=await _db.StockAdjustments.FindAsync([id],ct); if(e is null) return NotFound(); e.Status="Cancelled"; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","StockAdjustments",e.Uid.ToString(),$"Cancelled {e.AdjustmentNo}",ct); TempData["Success"]="Stock adjustment cancelled."; return RedirectToAction(nameof(Index)); }
}
