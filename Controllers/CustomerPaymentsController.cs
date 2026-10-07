using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class CustomerPaymentsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public CustomerPaymentsController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Customers=new SelectList(await _db.Customers.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CustomerName).ToListAsync(ct),"Uid","CustomerName");
    ViewBag.Invoices=new SelectList(await _db.SalesInvoices.AsNoTracking().OrderByDescending(x=>x.Uid).Take(200).ToListAsync(ct),"Uid","InvoiceNo");
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName");
  }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.CustomerPayments.AsNoTracking().OrderByDescending(x=>x.PaymentDate).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new CustomerPaymentFormViewModel{PaymentDate=DateTime.Now, PaymentNo=$"CP-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(CustomerPaymentFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=new CustomerPayment{PaymentNo=model.PaymentNo.Trim(),PaymentDate=model.PaymentDate,CustomerUid=model.CustomerUid,SalesInvoiceUid=model.SalesInvoiceUid,PaymentMethodUid=model.PaymentMethodUid,Amount=model.Amount,ReferenceNo=model.ReferenceNo,Remarks=model.Remarks,CreatedDate=DateTime.Now};
    _db.CustomerPayments.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Payment","CustomerPayments",e.Uid.ToString(),$"Customer payment {e.PaymentNo}",ct);
    TempData["Success"]="Customer payment saved."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.CustomerPayments.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(new CustomerPaymentFormViewModel{Uid=e.Uid,PaymentNo=e.PaymentNo,PaymentDate=e.PaymentDate,CustomerUid=e.CustomerUid,SalesInvoiceUid=e.SalesInvoiceUid,PaymentMethodUid=e.PaymentMethodUid,Amount=e.Amount,ReferenceNo=e.ReferenceNo,Remarks=e.Remarks}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, CustomerPaymentFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.CustomerPayments.FindAsync([id],ct); if(e is null) return NotFound();
    e.PaymentNo=model.PaymentNo.Trim(); e.PaymentDate=model.PaymentDate; e.CustomerUid=model.CustomerUid; e.SalesInvoiceUid=model.SalesInvoiceUid; e.PaymentMethodUid=model.PaymentMethodUid; e.Amount=model.Amount; e.ReferenceNo=model.ReferenceNo; e.Remarks=model.Remarks;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","CustomerPayments",e.Uid.ToString(),$"Updated payment {e.PaymentNo}",ct);
    TempData["Success"]="Customer payment updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.CustomerPayments.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
}
