using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class VendorPaymentsController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit;
  public VendorPaymentsController(POSDbContext db, IAuditService audit){ _db=db; _audit=audit; }
  private async Task LoadLookups(CancellationToken ct){
    ViewBag.Vendors=new SelectList(await _db.Vendors.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.VendorName).ToListAsync(ct),"Uid","VendorName");
    ViewBag.Invoices=new SelectList(await _db.PurchaseInvoices.AsNoTracking().OrderByDescending(x=>x.Uid).Take(200).ToListAsync(ct),"Uid","PurchaseInvoiceNo");
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName");
  }
  public async Task<IActionResult> Index(CancellationToken ct)=>View(await _db.VendorPayments.AsNoTracking().OrderByDescending(x=>x.PaymentDate).ToListAsync(ct));
  public async Task<IActionResult> Create(CancellationToken ct){ await LoadLookups(ct); return View(new VendorPaymentFormViewModel{PaymentDate=DateTime.Now, PaymentNo=$"VP-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(VendorPaymentFormViewModel model, CancellationToken ct){
    if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=new VendorPayment{PaymentNo=model.PaymentNo.Trim(),PaymentDate=model.PaymentDate,VendorUid=model.VendorUid,PurchaseInvoiceUid=model.PurchaseInvoiceUid,PaymentMethodUid=model.PaymentMethodUid,Amount=model.Amount,ReferenceNo=model.ReferenceNo,Remarks=model.Remarks,CreatedDate=DateTime.Now};
    _db.VendorPayments.Add(e); await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Payment","VendorPayments",e.Uid.ToString(),$"Vendor payment {e.PaymentNo}",ct);
    TempData["Success"]="Vendor payment saved."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){ var e=await _db.VendorPayments.FindAsync([id],ct); if(e is null) return NotFound(); await LoadLookups(ct); return View(new VendorPaymentFormViewModel{Uid=e.Uid,PaymentNo=e.PaymentNo,PaymentDate=e.PaymentDate,VendorUid=e.VendorUid,PurchaseInvoiceUid=e.PurchaseInvoiceUid,PaymentMethodUid=e.PaymentMethodUid,Amount=e.Amount,ReferenceNo=e.ReferenceNo,Remarks=e.Remarks}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, VendorPaymentFormViewModel model, CancellationToken ct){
    if(id!=model.Uid) return BadRequest(); if(!ModelState.IsValid){ await LoadLookups(ct); return View(model);} 
    var e=await _db.VendorPayments.FindAsync([id],ct); if(e is null) return NotFound();
    e.PaymentNo=model.PaymentNo.Trim(); e.PaymentDate=model.PaymentDate; e.VendorUid=model.VendorUid; e.PurchaseInvoiceUid=model.PurchaseInvoiceUid; e.PaymentMethodUid=model.PaymentMethodUid; e.Amount=model.Amount; e.ReferenceNo=model.ReferenceNo; e.Remarks=model.Remarks;
    await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Update","VendorPayments",e.Uid.ToString(),$"Updated payment {e.PaymentNo}",ct);
    TempData["Success"]="Vendor payment updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct){ var e=await _db.VendorPayments.AsNoTracking().FirstOrDefaultAsync(x=>x.Uid==id,ct); return e is null?NotFound():View(e);} 
}
