using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize(Policy = "ManagerOrAdmin")]
public class ExpensesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public ExpensesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Branches=new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct),"Uid","BranchName");
    ViewBag.Categories=new SelectList(await _db.ExpenseCategories.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CategoryName).ToListAsync(ct),"Uid","CategoryName");
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName");
  }
  public async Task<IActionResult> Index(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
  {
    ViewData["Title"] = "Expenses";
    var fromDay = dateFrom?.Date;
    var toExclusive = dateTo?.Date.AddDays(1);

    var query = _db.Expenses.AsNoTracking().AsQueryable();
    if (fromDay.HasValue)
      query = query.Where(x => x.ExpenseDate >= fromDay.Value);
    if (toExclusive.HasValue)
      query = query.Where(x => x.ExpenseDate < toExclusive.Value);

    ViewBag.DateFrom = fromDay;
    ViewBag.DateTo = dateTo?.Date;

    return View(await query.OrderByDescending(x => x.ExpenseDate).ThenByDescending(x => x.Uid).ToListAsync(ct));
  }
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

  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Delete(int id, DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
  {
    var e = await _db.Expenses.FindAsync([id], ct);
    if (e is null)
    {
      TempData["Error"] = "Expense was not found.";
      return RedirectToAction(nameof(Index), new { dateFrom, dateTo });
    }

    var expenseNo = e.ExpenseNo;
    try
    {
      _db.Expenses.Remove(e);
      await _db.SaveChangesAsync(ct);
      await _audit.WriteAsync("Delete", "Expenses", id.ToString(), $"Deleted expense {expenseNo}", ct);
      TempData["Success"] = $"Expense {expenseNo} deleted.";
    }
    catch (DbUpdateException)
    {
      TempData["Error"] = $"Cannot delete expense {expenseNo} because other records still use it.";
    }

    return RedirectToAction(nameof(Index), new { dateFrom, dateTo });
  }

  /// <summary>Creates (or reactivates) an expense category for the Create/Edit dropdown.</summary>
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> AddCategory([FromForm] string? categoryName, CancellationToken ct)
  {
    var name = (categoryName ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(name))
      return BadRequest(new { success = false, message = "Category name is required." });

    if (name.Length > 150)
      return BadRequest(new { success = false, message = "Category name is too long." });

    var existing = await _db.ExpenseCategories
      .FirstOrDefaultAsync(x => x.CategoryName == name, ct);

    if (existing is null)
    {
      // Case-insensitive match when DB collation is case-insensitive; otherwise scan.
      var all = await _db.ExpenseCategories.AsNoTracking().Select(x => new { x.Uid, x.CategoryName, x.IsActive }).ToListAsync(ct);
      var match = all.FirstOrDefault(x => string.Equals(x.CategoryName, name, StringComparison.OrdinalIgnoreCase));
      if (match is not null)
        existing = await _db.ExpenseCategories.FirstOrDefaultAsync(x => x.Uid == match.Uid, ct);
    }

    if (existing is not null)
    {
      if (!existing.IsActive)
      {
        existing.IsActive = true;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "ExpenseCategories", existing.Uid.ToString(),
          $"Reactivated category {existing.CategoryCode}", ct);
      }

      return Ok(new
      {
        success = true,
        uid = existing.Uid,
        name = existing.CategoryName,
        created = false,
        message = "Category already exists."
      });
    }

    var code = BuildCategoryCode(name);
    var n = 1;
    while (await _db.ExpenseCategories.AnyAsync(x => x.CategoryCode == code, ct))
    {
      var suffix = n.ToString();
      var baseLen = Math.Min(50 - 1 - suffix.Length, code.Length);
      code = $"{code[..baseLen]}-{suffix}";
      n++;
      if (n > 99)
      {
        code = $"EXP-{DateTime.Now:yyyyMMddHHmmss}";
        break;
      }
    }

    var entity = new ExpenseCategory
    {
      CategoryCode = code,
      CategoryName = name,
      IsActive = true
    };
    _db.ExpenseCategories.Add(entity);
    await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Create", "ExpenseCategories", entity.Uid.ToString(),
      $"Created {entity.CategoryCode} from Expenses form", ct);

    return Ok(new
    {
      success = true,
      uid = entity.Uid,
      name = entity.CategoryName,
      created = true,
      message = "Category added."
    });
  }

  private static string BuildCategoryCode(string categoryName)
  {
    var chars = categoryName
      .Where(ch => char.IsLetterOrDigit(ch))
      .Select(char.ToUpperInvariant)
      .Take(50)
      .ToArray();
    var code = new string(chars);
    return string.IsNullOrWhiteSpace(code) ? $"EXP-{DateTime.Now:yyyyMMddHHmmss}" : code;
  }
}
