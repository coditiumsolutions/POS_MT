using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class ExpensesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public ExpensesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Branches=new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct),"Uid","BranchName");
    ViewBag.Categories=new SelectList(await _db.ExpenseCategories.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CategoryName).ToListAsync(ct),"Uid","CategoryName");
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName");
  }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.Expenses.AsNoTracking().OrderByDescending(x=>x.ExpenseDate).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new ExpenseFormViewModel{ExpenseDate=DateTime.Now, ExpenseNo=$"EX-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(ExpenseFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=new Expense{ExpenseNo=model.ExpenseNo.Trim(),ExpenseDate=model.ExpenseDate,BranchUid=model.BranchUid,ExpenseCategoryUid=model.ExpenseCategoryUid,Description=model.Description,Amount=model.Amount,PaymentMethodUid=model.PaymentMethodUid,ReferenceNo=model.ReferenceNo,Remarks=model.Remarks,CreatedDate=DateTime.Now};
    _db.Expenses.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Create","Expenses",e.Uid.ToString(),$"Created expense {e.ExpenseNo}",ct);
    TempData["Success"]="Expense created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.Expenses.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(new ExpenseFormViewModel{Uid=e.Uid,ExpenseNo=e.ExpenseNo,ExpenseDate=e.ExpenseDate,BranchUid=e.BranchUid,ExpenseCategoryUid=e.ExpenseCategoryUid,Description=e.Description,Amount=e.Amount,PaymentMethodUid=e.PaymentMethodUid,ReferenceNo=e.ReferenceNo,Remarks=e.Remarks}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, ExpenseFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.Expenses.FindAsync([id],ct); if(e is null) return NotFound();
    e.ExpenseNo=model.ExpenseNo.Trim(); e.ExpenseDate=model.ExpenseDate; e.BranchUid=model.BranchUid; e.ExpenseCategoryUid=model.ExpenseCategoryUid; e.Description=model.Description; e.Amount=model.Amount; e.PaymentMethodUid=model.PaymentMethodUid; e.ReferenceNo=model.ReferenceNo; e.Remarks=model.Remarks;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","Expenses",e.Uid.ToString(),$"Updated expense {e.ExpenseNo}",ct);
    TempData["Success"]="Expense updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.Expenses.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
}
