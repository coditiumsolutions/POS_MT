using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class SalesInvoicesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public SalesInvoicesController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Branches=new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct),"Uid","BranchName");
    ViewBag.Customers=new SelectList(await _db.Customers.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CustomerName).ToListAsync(ct),"Uid","CustomerName");
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName");
  }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.SalesInvoices.AsNoTracking().OrderByDescending(x=>x.InvoiceDate).ThenByDescending(x=>x.Uid).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new SalesInvoiceFormViewModel{InvoiceDate=DateTime.Now, InvoiceNo=$"SI-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(SalesInvoiceFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    if(await _db.SalesInvoices.AnyAsync(x=>x.InvoiceNo==model.InvoiceNo,ct)){ ModelState.AddModelError(nameof(model.InvoiceNo),"Invoice no already exists."); await LoadLookups(ct); return View(model);} 
    var e=ToEntity(model); e.CreatedDate=DateTime.Now; _db.SalesInvoices.Add(e); await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Create","SalesInvoices",e.Uid.ToString(),$"Created sales invoice {e.InvoiceNo}",ct);
    TempData["Success"]="Sales invoice created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.SalesInvoices.FindAsync([id],ct); if(e is null) return NotFound(); if(e.InvoiceStatus=="Cancelled"){ TempData["Error"]="Cancelled invoices cannot be edited."; return RedirectToAction(nameof(Index)); } await LoadLookups(ct); return View(ToForm(e)); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, SalesInvoiceFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.SalesInvoices.FindAsync([id],ct); if(e is null) return NotFound(); if(e.InvoiceStatus=="Cancelled"){ TempData["Error"]="Cancelled invoices cannot be edited."; return RedirectToAction(nameof(Index)); } 
    Apply(e,model); e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","SalesInvoices",e.Uid.ToString(),$"Updated sales invoice {e.InvoiceNo}",ct);
    TempData["Success"]="Sales invoice updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.SalesInvoices.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Cancel(int id, CancellationToken ct){ var e=await _db.SalesInvoices.FindAsync([id],ct); if(e is null) return NotFound(); e.InvoiceStatus="Cancelled"; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","SalesInvoices",e.Uid.ToString(),$"Cancelled sales invoice {e.InvoiceNo}",ct); TempData["Success"]="Sales invoice cancelled."; return RedirectToAction(nameof(Index)); }
  private static SalesInvoiceFormViewModel ToForm(SalesInvoice e)=>new(){Uid=e.Uid,InvoiceNo=e.InvoiceNo,InvoiceDate=e.InvoiceDate,BranchUid=e.BranchUid,CustomerUid=e.CustomerUid,PaymentMethodUid=e.PaymentMethodUid,SubTotal=e.SubTotal,DiscountAmount=e.DiscountAmount,TaxAmount=e.TaxAmount,OtherCharges=e.OtherCharges,NetAmount=e.NetAmount,PaidAmount=e.PaidAmount,BalanceAmount=e.BalanceAmount,PaymentStatus=e.PaymentStatus,InvoiceStatus=e.InvoiceStatus,Remarks=e.Remarks};
  private static SalesInvoice ToEntity(SalesInvoiceFormViewModel m)=>new(){InvoiceNo=m.InvoiceNo.Trim(),InvoiceDate=m.InvoiceDate,BranchUid=m.BranchUid,CustomerUid=m.CustomerUid,PaymentMethodUid=m.PaymentMethodUid,SubTotal=m.SubTotal,DiscountAmount=m.DiscountAmount,TaxAmount=m.TaxAmount,OtherCharges=m.OtherCharges,NetAmount=m.NetAmount,PaidAmount=m.PaidAmount,BalanceAmount=m.BalanceAmount,PaymentStatus=m.PaymentStatus,InvoiceStatus=m.InvoiceStatus,Remarks=m.Remarks};
  private static void Apply(SalesInvoice e, SalesInvoiceFormViewModel m){ e.InvoiceNo=m.InvoiceNo.Trim(); e.InvoiceDate=m.InvoiceDate; e.BranchUid=m.BranchUid; e.CustomerUid=m.CustomerUid; e.PaymentMethodUid=m.PaymentMethodUid; e.SubTotal=m.SubTotal; e.DiscountAmount=m.DiscountAmount; e.TaxAmount=m.TaxAmount; e.OtherCharges=m.OtherCharges; e.NetAmount=m.NetAmount; e.PaidAmount=m.PaidAmount; e.BalanceAmount=m.BalanceAmount; e.PaymentStatus=m.PaymentStatus; e.InvoiceStatus=m.InvoiceStatus; e.Remarks=m.Remarks; }
}
