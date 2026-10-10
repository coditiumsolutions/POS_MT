using Microsoft.AspNetCore.Authorization; using Microsoft.AspNetCore.Mvc; using Microsoft.AspNetCore.Mvc.Rendering; using Microsoft.EntityFrameworkCore;
using POS_MT.Data; using POS_MT.Interfaces; using POS_MT.Models; using POS_MT.Services; using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class SalesInvoicesController : Controller {
  private readonly POSDbContext _db; private readonly IAuditService _audit; private readonly INavContextService _navContext;
  public SalesInvoicesController(POSDbContext db, IAuditService audit, INavContextService navContext){ _db=db; _audit=audit; _navContext=navContext; }
  private void EnsureSalesInvoiceNav() => _navContext.SetArea("SalesInvoice");
  private async Task LoadLookups(CancellationToken ct, int? selectedBranchUid = null, int? selectedCustomerUid = null, int? selectedPaymentMethodUid = null){
    ViewBag.Branches=new SelectList(await _db.Branches.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.BranchName).ToListAsync(ct),"Uid","BranchName", selectedBranchUid);
    ViewBag.Customers=new SelectList(await _db.Customers.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.CustomerName).ToListAsync(ct),"Uid","CustomerName", selectedCustomerUid);
    ViewBag.PaymentMethods=new SelectList(await _db.PaymentMethods.AsNoTracking().Where(x=>x.IsActive).OrderBy(x=>x.PaymentName).ToListAsync(ct),"Uid","PaymentName", selectedPaymentMethodUid);
  }
  public async Task<IActionResult> Index(string? customerType, int? month, string? search, CancellationToken ct)
  {
    EnsureSalesInvoiceNav();
    ViewData["Title"] = "Sales Invoices";

    var query =
      from inv in _db.SalesInvoices.AsNoTracking()
      join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
      from c in cj.DefaultIfEmpty()
      select new { inv, c };

    if (string.Equals(customerType, "walkin", StringComparison.OrdinalIgnoreCase))
    {
      query = query.Where(x => x.c != null &&
        (x.c.CustomerCode == "C001" ||
         x.c.CustomerName.Contains("Walk-in") ||
         x.c.CustomerName.Contains("Walk-In") ||
         x.c.CustomerName.Contains("Walk In")));
    }
    else if (string.Equals(customerType, "credit", StringComparison.OrdinalIgnoreCase))
    {
      query = query.Where(x => x.c != null &&
        x.c.CustomerCode != "C001" &&
        !x.c.CustomerName.Contains("Walk-in") &&
        !x.c.CustomerName.Contains("Walk-In") &&
        !x.c.CustomerName.Contains("Walk In") &&
        (x.inv.Remarks == null || !x.inv.Remarks.StartsWith("Monthly Supply")));
    }
    else if (string.Equals(customerType, "monthly", StringComparison.OrdinalIgnoreCase))
    {
      query = query.Where(x => x.c != null &&
        x.c.CustomerCode != "C001" &&
        !x.c.CustomerName.Contains("Walk-in") &&
        !x.c.CustomerName.Contains("Walk-In") &&
        !x.c.CustomerName.Contains("Walk In") &&
        x.inv.Remarks != null &&
        x.inv.Remarks.StartsWith("Monthly Supply"));
    }

    if (month is >= 1 and <= 12)
    {
      query = query.Where(x => x.inv.InvoiceDate.Month == month.Value);
    }

    if (!string.IsNullOrWhiteSpace(search))
    {
      var term = search.Trim();
      query = query.Where(x =>
        x.inv.InvoiceNo.Contains(term) ||
        x.inv.InvoiceStatus.Contains(term) ||
        x.inv.PaymentStatus.Contains(term) ||
        (x.inv.Remarks != null && x.inv.Remarks.Contains(term)) ||
        (x.c != null && x.c.CustomerName.Contains(term)) ||
        (x.c != null && x.c.CustomerCode.Contains(term)));
    }

    var list = await query
      .OrderByDescending(x => x.inv.InvoiceDate)
      .ThenByDescending(x => x.inv.Uid)
      .Select(x => new SalesInvoiceListItemViewModel
      {
        Uid = x.inv.Uid,
        InvoiceNo = x.inv.InvoiceNo,
        InvoiceDate = x.inv.InvoiceDate,
        CustomerName = x.c != null ? x.c.CustomerName : "—",
        CustomerType = x.c == null
          ? "—"
          : (x.c.CustomerCode == "C001" ||
             x.c.CustomerName.Contains("Walk-in") ||
             x.c.CustomerName.Contains("Walk-In") ||
             x.c.CustomerName.Contains("Walk In")
              ? "Walk-in"
              : (x.inv.Remarks != null && x.inv.Remarks.StartsWith("Monthly Supply")
                  ? "Monthly"
                  : "Credit")),
        NetAmount = x.inv.NetAmount,
        PaidAmount = x.inv.PaidAmount,
        InvoiceStatus = x.inv.InvoiceStatus,
        PaymentStatus = x.inv.PaymentStatus
      }).ToListAsync(ct);

    return View(new SalesInvoiceIndexViewModel
    {
      CustomerType = customerType,
      Month = month is >= 1 and <= 12 ? month : null,
      Search = search,
      Rows = list
    });
  }
  public async Task<IActionResult> Create(CancellationToken ct){ EnsureSalesInvoiceNav(); await LoadLookups(ct); return View(new SalesInvoiceFormViewModel{InvoiceDate=DateTime.Now, InvoiceNo=$"SI-{DateTime.Now:yyyyMMddHHmmss}"}); }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Create(SalesInvoiceFormViewModel model, CancellationToken ct){
    EnsureSalesInvoiceNav();
    if(!ModelState.IsValid){ await LoadLookups(ct, model.BranchUid, model.CustomerUid, model.PaymentMethodUid); return View(model);} 
    if(await _db.SalesInvoices.AnyAsync(x=>x.InvoiceNo==model.InvoiceNo,ct)){ ModelState.AddModelError(nameof(model.InvoiceNo),"Invoice no already exists."); await LoadLookups(ct, model.BranchUid, model.CustomerUid, model.PaymentMethodUid); return View(model);} 
    var e=ToEntity(model); e.CreatedDate=DateTime.Now; _db.SalesInvoices.Add(e); await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Create","SalesInvoices",e.Uid.ToString(),$"Created sales invoice {e.InvoiceNo}",ct);
    TempData["Success"]="Sales invoice created."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Edit(int id, CancellationToken ct){
    EnsureSalesInvoiceNav();
    var e=await _db.SalesInvoices.FindAsync([id],ct);
    if(e is null) return NotFound();
    if(e.InvoiceStatus=="Cancelled"){ TempData["Error"]="Cancelled invoices cannot be edited."; return RedirectToAction(nameof(Index)); }
    await LoadLookups(ct, e.BranchUid, e.CustomerUid, e.PaymentMethodUid);
    return View(ToForm(e));
  }
  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Edit(int id, SalesInvoiceFormViewModel model, CancellationToken ct){
    EnsureSalesInvoiceNav();
    if(id!=model.Uid) return BadRequest();
    var e=await _db.SalesInvoices.FindAsync([id],ct);
    if(e is null) return NotFound();
    if(e.InvoiceStatus=="Cancelled"){ TempData["Error"]="Cancelled invoices cannot be edited."; return RedirectToAction(nameof(Index)); }
    if(model.PaidAmount < 0)
    {
      ModelState.AddModelError(nameof(model.PaidAmount), "Paid amount cannot be negative.");
    }
    if(!ModelState.IsValid){
      // Restore readonly display values from entity so the form stays consistent.
      model.InvoiceNo = e.InvoiceNo;
      model.InvoiceDate = e.InvoiceDate;
      model.CustomerUid = e.CustomerUid;
      model.SubTotal = e.SubTotal;
      model.DiscountAmount = e.DiscountAmount;
      model.TaxAmount = e.TaxAmount;
      model.OtherCharges = e.OtherCharges;
      model.NetAmount = e.NetAmount;
      model.BalanceAmount = e.BalanceAmount;
      model.PaymentStatus = e.PaymentStatus;
      model.InvoiceStatus = e.InvoiceStatus;
      await LoadLookups(ct, model.BranchUid, e.CustomerUid, model.PaymentMethodUid);
      return View(model);
    }
    ApplyEditable(e, model);
    e.UpdatedDate=DateTime.Now;
    await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Update","SalesInvoices",e.Uid.ToString(),$"Updated sales invoice {e.InvoiceNo}",ct);
    TempData["Success"]="Sales invoice updated."; return RedirectToAction(nameof(Index));
  }
  public async Task<IActionResult> Details(int id, CancellationToken ct)
  {
    EnsureSalesInvoiceNav();
    var model = await LoadDetailsPageAsync(id, ct);
    if (model is null) return NotFound();
    return View(model);
  }

  public async Task<IActionResult> Print(int id, CancellationToken ct)
  {
    EnsureSalesInvoiceNav();
    var model = await LoadDetailsPageAsync(id, ct);
    if (model is null) return NotFound();
    return View(model);
  }

  private async Task<SalesInvoiceDetailsPageViewModel?> LoadDetailsPageAsync(int id, CancellationToken ct)
  {
    var row = await (
      from inv in _db.SalesInvoices.AsNoTracking()
      join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
      from c in cj.DefaultIfEmpty()
      where inv.Uid == id
      select new { inv, c }
    ).FirstOrDefaultAsync(ct);

    if (row is null) return null;

    var lines = await _db.SalesInvoiceDetails.AsNoTracking()
      .Where(x => x.SalesInvoiceUid == id)
      .OrderBy(x => x.Uid)
      .Select(x => new SalesInvoiceDetailLineViewModel
      {
        DetailUid = x.Uid,
        ProductCode = x.ProductCode ?? string.Empty,
        ProductName = x.ProductName ?? string.Empty,
        Quantity = x.Quantity,
        UnitPrice = x.UnitPrice,
        DiscountAmount = x.DiscountAmount,
        TaxAmount = x.TaxAmount,
        LineTotal = x.LineTotal,
        Remarks = x.Remarks
      })
      .ToListAsync(ct);

    var customer = row.c;
    var customerTypeLabel = CustomerClassification.GetSalesTypeLabel(
      customer?.CustomerCode,
      customer?.CustomerName,
      row.inv.Remarks);

    return new SalesInvoiceDetailsPageViewModel
    {
      Uid = row.inv.Uid,
      InvoiceNo = row.inv.InvoiceNo,
      InvoiceDate = row.inv.InvoiceDate,
      CustomerCode = customer?.CustomerCode ?? "—",
      CustomerName = customer?.CustomerName ?? "—",
      CustomerType = customerTypeLabel,
      SubTotal = row.inv.SubTotal,
      DiscountAmount = row.inv.DiscountAmount,
      TaxAmount = row.inv.TaxAmount,
      OtherCharges = row.inv.OtherCharges,
      NetAmount = row.inv.NetAmount,
      PaidAmount = row.inv.PaidAmount,
      BalanceAmount = row.inv.BalanceAmount,
      InvoiceStatus = row.inv.InvoiceStatus,
      PaymentStatus = row.inv.PaymentStatus,
      Remarks = row.inv.Remarks,
      Lines = lines
    };
  }

  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Cancel(int id, CancellationToken ct){ EnsureSalesInvoiceNav(); var e=await _db.SalesInvoices.FindAsync([id],ct); if(e is null) return NotFound(); e.InvoiceStatus="Cancelled"; e.UpdatedDate=DateTime.Now; await _db.SaveChangesAsync(ct); await _audit.WriteAsync("Cancel","SalesInvoices",e.Uid.ToString(),$"Cancelled sales invoice {e.InvoiceNo}",ct); TempData["Success"]="Sales invoice cancelled."; return RedirectToAction(nameof(Index)); }

  [HttpPost, ValidateAntiForgeryToken]
  public async Task<IActionResult> Delete(int id, string? customerType, int? month, string? search, CancellationToken ct)
  {
    EnsureSalesInvoiceNav();
    var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Uid == id, ct);
    if (invoice is null)
    {
      TempData["Error"] = "Sales invoice was not found.";
      return RedirectToAction(nameof(Index), new { customerType, month, search });
    }

    var invoiceNo = invoice.InvoiceNo;
    var details = await _db.SalesInvoiceDetails.Where(x => x.SalesInvoiceUid == id).ToListAsync(ct);
    if (details.Count > 0)
    {
      _db.SalesInvoiceDetails.RemoveRange(details);
    }

    _db.SalesInvoices.Remove(invoice);
    await _db.SaveChangesAsync(ct);
    await _audit.WriteAsync("Delete", "SalesInvoices", id.ToString(),
      $"Deleted sales invoice {invoiceNo} and {details.Count} detail line(s).", ct);

    TempData["Success"] = $"Sales invoice {invoiceNo} deleted.";
    return RedirectToAction(nameof(Index), new { customerType, month, search });
  }
  private static SalesInvoiceFormViewModel ToForm(SalesInvoice e)=>new(){Uid=e.Uid,InvoiceNo=e.InvoiceNo,InvoiceDate=e.InvoiceDate,BranchUid=e.BranchUid,CustomerUid=e.CustomerUid,PaymentMethodUid=e.PaymentMethodUid,SubTotal=e.SubTotal,DiscountAmount=e.DiscountAmount,TaxAmount=e.TaxAmount,OtherCharges=e.OtherCharges,NetAmount=e.NetAmount,PaidAmount=e.PaidAmount,BalanceAmount=e.BalanceAmount,PaymentStatus=e.PaymentStatus,InvoiceStatus=e.InvoiceStatus,Remarks=e.Remarks};
  private static SalesInvoice ToEntity(SalesInvoiceFormViewModel m)=>new(){InvoiceNo=m.InvoiceNo.Trim(),InvoiceDate=m.InvoiceDate,BranchUid=m.BranchUid,CustomerUid=m.CustomerUid,PaymentMethodUid=m.PaymentMethodUid,SubTotal=m.SubTotal,DiscountAmount=m.DiscountAmount,TaxAmount=m.TaxAmount,OtherCharges=m.OtherCharges,NetAmount=m.NetAmount,PaidAmount=m.PaidAmount,BalanceAmount=m.BalanceAmount,PaymentStatus=m.PaymentStatus,InvoiceStatus=m.InvoiceStatus,Remarks=m.Remarks};
  private static void ApplyEditable(SalesInvoice e, SalesInvoiceFormViewModel m)
  {
    e.BranchUid = m.BranchUid;
    e.PaymentMethodUid = m.PaymentMethodUid;
    e.PaidAmount = m.PaidAmount;
    e.Remarks = string.IsNullOrWhiteSpace(m.Remarks) ? null : m.Remarks.Trim();
    e.BalanceAmount = e.NetAmount - e.PaidAmount;
    e.PaymentStatus = e.PaidAmount <= 0
      ? "Unpaid"
      : e.PaidAmount >= e.NetAmount
        ? "Paid"
        : "Partial";
  }
}
