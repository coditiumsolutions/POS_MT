using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class VendorsController : Controller
{
    private const string VendorCodePrefix = "VEN-";
    private const int VendorCodeStartNumber = 1000;

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;

    public VendorsController(POSDbContext db, IAuditService audit, INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
    }

    private void EnsureVendorsNav() => _navContext.SetArea("Vendors");
    private void MarkVendorCodeReadonly() => ViewBag.VendorCodeReadonly = true;

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureVendorsNav();
        return View(await _db.Vendors.AsNoTracking().OrderBy(x => x.VendorName).ToListAsync(ct));
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureVendorsNav();
        MarkVendorCodeReadonly();
        var model = new VendorFormViewModel
        {
            IsActive = true,
            VendorCode = await GenerateNextVendorCodeAsync(ct)
        };
        await PopulateCityOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        MarkVendorCodeReadonly();

        // Always assign server-side so codes stay sequential and unique.
        model.VendorCode = await GenerateNextVendorCodeAsync(ct);
        ModelState.Remove(nameof(model.VendorCode));

        if (!ModelState.IsValid)
        {
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }

        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode, ct))
        {
            model.VendorCode = await GenerateNextVendorCodeAsync(ct);
            if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode, ct))
            {
                ModelState.AddModelError(nameof(model.VendorCode), "Could not generate a unique vendor code. Please try again.");
                await PopulateCityOptionsAsync(model, ct);
                return View(model);
            }
        }

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;
        _db.Vendors.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Vendors", e.Uid.ToString(), $"Created vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        var model = ToForm(e);
        await PopulateCityOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, VendorFormViewModel model, CancellationToken ct)
    {
        EnsureVendorsNav();
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid)
        {
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        if (await _db.Vendors.AnyAsync(x => x.VendorCode == model.VendorCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.VendorCode), "Vendor code already exists.");
            await PopulateCityOptionsAsync(model, ct);
            return View(model);
        }
        Apply(e, model);
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Vendors", e.Uid.ToString(), $"Updated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return e is null ? NotFound() : View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.IsActive = false;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "Vendors", e.Uid.ToString(), $"Deactivated vendor {e.VendorCode}", ct);
        TempData["Success"] = "Vendor deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        EnsureVendorsNav();
        var e = await _db.Vendors.FindAsync([id], ct);
        if (e is null)
        {
            TempData["Error"] = "Vendor was not found.";
            return RedirectToAction(nameof(Index));
        }

        var code = e.VendorCode;
        var purchaseCount = await _db.PurchaseInvoices.CountAsync(x => x.VendorUid == id, ct);
        if (purchaseCount > 0)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because {purchaseCount} purchase invoice(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        var paymentCount = await _db.VendorPayments.CountAsync(x => x.VendorUid == id, ct);
        if (paymentCount > 0)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because {paymentCount} vendor payment(s) still use it.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.Vendors.Remove(e);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "Vendors", id.ToString(), $"Deleted vendor {code}", ct);
            TempData["Success"] = $"Vendor {code} deleted.";
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = $"Cannot delete vendor {code} because other records still use it.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string> GenerateNextVendorCodeAsync(CancellationToken ct)
    {
        var existing = await _db.Vendors.AsNoTracking()
            .Where(x => x.VendorCode.StartsWith(VendorCodePrefix))
            .Select(x => x.VendorCode)
            .ToListAsync(ct);

        var maxNumber = VendorCodeStartNumber - 1;
        foreach (var code in existing)
        {
            var suffix = code.Length > VendorCodePrefix.Length
                ? code[VendorCodePrefix.Length..]
                : string.Empty;
            if (int.TryParse(suffix, out var number) && number > maxNumber)
            {
                maxNumber = number;
            }
        }

        return $"{VendorCodePrefix}{maxNumber + 1}";
    }

    private static VendorFormViewModel ToForm(Vendor e) => new()
    {
        Uid = e.Uid,
        VendorCode = e.VendorCode,
        VendorName = e.VendorName,
        ContactPerson = e.ContactPerson,
        MobileNo = e.MobileNo,
        PhoneNo = e.PhoneNo,
        Email = e.Email,
        Address = e.Address,
        City = e.City,
        Ntn = e.Ntn,
        Strn = e.Strn,
        OpeningBalance = e.OpeningBalance,
        CreditLimit = e.CreditLimit,
        PaymentTerms = e.PaymentTerms,
        IsActive = e.IsActive
    };

    private static Vendor ToEntity(VendorFormViewModel m) => new()
    {
        VendorCode = m.VendorCode.Trim(),
        VendorName = m.VendorName.Trim(),
        ContactPerson = m.ContactPerson,
        MobileNo = m.MobileNo,
        PhoneNo = m.PhoneNo,
        Email = m.Email,
        Address = m.Address,
        City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim(),
        Ntn = m.Ntn,
        Strn = m.Strn,
        OpeningBalance = m.OpeningBalance,
        CreditLimit = m.CreditLimit,
        PaymentTerms = m.PaymentTerms,
        IsActive = m.IsActive
    };

    private static void Apply(Vendor e, VendorFormViewModel m)
    {
        e.VendorCode = m.VendorCode.Trim();
        e.VendorName = m.VendorName.Trim();
        e.ContactPerson = m.ContactPerson;
        e.MobileNo = m.MobileNo;
        e.PhoneNo = m.PhoneNo;
        e.Email = m.Email;
        e.Address = m.Address;
        e.City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim();
        e.Ntn = m.Ntn;
        e.Strn = m.Strn;
        e.OpeningBalance = m.OpeningBalance;
        e.CreditLimit = m.CreditLimit;
        e.PaymentTerms = m.PaymentTerms;
        e.IsActive = m.IsActive;
    }

    private async Task PopulateCityOptionsAsync(VendorFormViewModel model, CancellationToken ct)
    {
        var citiesRaw = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == "Cities")
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        model.CityOptions = BuildCityOptions(citiesRaw, model.City);
    }

    private static List<SelectListItem> BuildCityOptions(string? csv, string? selected)
    {
        var values = (csv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(selected)
            && !values.Any(x => string.Equals(x, selected, StringComparison.OrdinalIgnoreCase)))
        {
            values.Insert(0, selected.Trim());
        }

        var items = new List<SelectListItem>
        {
            new() { Value = "", Text = "Select city…", Selected = string.IsNullOrWhiteSpace(selected) }
        };

        items.AddRange(values.Select(v => new SelectListItem
        {
            Value = v,
            Text = v,
            Selected = !string.IsNullOrWhiteSpace(selected)
                       && string.Equals(v, selected, StringComparison.OrdinalIgnoreCase)
        }));

        return items;
    }
}
