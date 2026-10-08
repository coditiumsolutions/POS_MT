using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;
namespace POS_MT.Controllers;
[Authorize]
public class VendorsController : Controller
{
    private readonly POSDbContext _db; private readonly IAuditService _audit; private readonly INavContextService _navContext;
    public VendorsController(POSDbContext db, IAuditService audit, INavContextService navContext) { _db = db; _audit = audit; _navContext = navContext; }
    private void EnsureVendorsNav() => _navContext.SetArea("Vendors");
    public async Task<IActionResult> Index(CancellationToken ct) { EnsureVendorsNav(); return View(await _db.Vendors.AsNoTracking().OrderBy(x => x.VendorName).ToListAsync(ct)); }
    public IActionResult Create() { EnsureVendorsNav(); return View(new VendorFormViewModel { IsActive = true }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        if (!ModelState.IsValid) return View(model);
        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode, ct)) { ModelState.AddModelError(nameof(model.VendorCode), "Vendor code already exists."); return View(model); }
        var e = ToEntity(model); e.CreatedDate = DateTime.Now; _db.Vendors.Add(e); await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Vendors", e.Uid.ToString(), $"Created vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor created successfully."; return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Edit(int id, CancellationToken ct) { EnsureVendorsNav(); var e = await _db.Vendors.FindAsync([id], ct); return e is null ? NotFound() : View(ToForm(e)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var e = await _db.Vendors.FindAsync([id], ct); if (e is null) return NotFound();
        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode && x.Uid != id, ct)) { ModelState.AddModelError(nameof(model.VendorCode), "Vendor code already exists."); return View(model); }
        Apply(e, model); e.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Vendors", e.Uid.ToString(), $"Updated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor updated successfully."; return RedirectToAction(nameof(Index));
    }
    public async Task<IActionResult> Details(int id, CancellationToken ct) { EnsureVendorsNav(); var e = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct); return e is null ? NotFound() : View(e); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct); if (e is null) return NotFound();
        e.IsActive = false; e.UpdatedDate = DateTime.Now; await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "Vendors", e.Uid.ToString(), $"Deactivated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor deactivated."; return RedirectToAction(nameof(Index));
    }
    private static VendorFormViewModel ToForm(Vendor e) => new() { Uid=e.Uid, VendorCode=e.VendorCode, VendorName=e.VendorName, ContactPerson=e.ContactPerson, MobileNo=e.MobileNo, PhoneNo=e.PhoneNo, Email=e.Email, Address=e.Address, City=e.City, Ntn=e.Ntn, Strn=e.Strn, OpeningBalance=e.OpeningBalance, CreditLimit=e.CreditLimit, PaymentTerms=e.PaymentTerms, IsActive=e.IsActive };
    private static Vendor ToEntity(VendorFormViewModel m) => new() { VendorCode=m.VendorCode.Trim(), VendorName=m.VendorName.Trim(), ContactPerson=m.ContactPerson, MobileNo=m.MobileNo, PhoneNo=m.PhoneNo, Email=m.Email, Address=m.Address, City=m.City, Ntn=m.Ntn, Strn=m.Strn, OpeningBalance=m.OpeningBalance, CreditLimit=m.CreditLimit, PaymentTerms=m.PaymentTerms, IsActive=m.IsActive };
    private static void Apply(Vendor e, VendorFormViewModel m) { e.VendorCode=m.VendorCode.Trim(); e.VendorName=m.VendorName.Trim(); e.ContactPerson=m.ContactPerson; e.MobileNo=m.MobileNo; e.PhoneNo=m.PhoneNo; e.Email=m.Email; e.Address=m.Address; e.City=m.City; e.Ntn=m.Ntn; e.Strn=m.Strn; e.OpeningBalance=m.OpeningBalance; e.CreditLimit=m.CreditLimit; e.PaymentTerms=m.PaymentTerms; e.IsActive=m.IsActive; }
}
