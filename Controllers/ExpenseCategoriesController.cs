using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class ExpenseCategoriesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public ExpenseCategoriesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.ExpenseCategories.AsNoTracking().OrderBy(x=>x.CategoryName).ToListAsync(ct));
  public IActionResult Create()=>View(new ExpenseCategoryFormViewModel{IsActive=true});
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(ExpenseCategoryFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid) return View(model);
    if(await _db.ExpenseCategories.AnyAsync(x=>x.CategoryCode==model.CategoryCode,ct)){ ModelState.AddModelError(nameof(model.CategoryCode),"Code already exists."); return View(model);} 
    var e=new ExpenseCategory{CategoryCode=model.CategoryCode.Trim(),CategoryName=model.CategoryName.Trim(),Description=model.Description,IsActive=model.IsActive};
    _db.ExpenseCategories.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","ExpenseCategories",e.Uid.ToString(),$"Created {e.CategoryCode}",ct);
    TempData["Success"]="Expense category created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.ExpenseCategories.FindAsync([id],ct); return e is null?NotFound():View(new ExpenseCategoryFormViewModel{Uid=e.Uid,CategoryCode=e.CategoryCode,CategoryName=e.CategoryName,Description=e.Description,IsActive=e.IsActive}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, ExpenseCategoryFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid) return View(model);
    var e=await _db.ExpenseCategories.FindAsync([id],ct); if(e is null) return NotFound();
    e.CategoryCode=model.CategoryCode.Trim(); e.CategoryName=model.CategoryName.Trim(); e.Description=model.Description; e.IsActive=model.IsActive;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","ExpenseCategories",e.Uid.ToString(),$"Updated {e.CategoryCode}",ct);
    TempData["Success"]="Expense category updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.ExpenseCategories.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Deactivate(int id, CancellationToken ct){ var e=await _db.ExpenseCategories.FindAsync([id],ct); if(e is null) return NotFound(); e.IsActive=false; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","ExpenseCategories",e.Uid.ToString(),$"Deactivated {e.CategoryCode}",ct); TempData["Success"]="Category deactivated."; return RedirectToAction(nameof(Index)); }
}
