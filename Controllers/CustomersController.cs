using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class CustomersController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;

    public CustomersController(POSDbContext db, IAuditService audit, INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
    }

    private void EnsureCustomersNav() => _navContext.SetArea("Customers");

    public async Task<IActionResult> Index(string? filter, CancellationToken ct)
    {
        EnsureCustomersNav();
        ViewData["Filter"] = filter;
        var query = _db.Customers.AsNoTracking().AsQueryable();
        if (string.Equals(filter, "credit", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CreditLimit > 0);
            ViewData["Title"] = "Credit Customers";
        }
        else if (string.Equals(filter, "cash", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(x => x.CreditLimit <= 0);
            ViewData["Title"] = "Cash Customers";
        }
        else
        {
            ViewData["Title"] = "Customers";
        }

        var list = await query.OrderBy(x => x.CustomerName).ToListAsync(ct);
        return View(list);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureCustomersNav();
        var model = new CustomerFormViewModel { IsActive = true };
        await PopulateCityAreaOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        if (!ModelState.IsValid)
        {
            await PopulateCityAreaOptionsAsync(model, ct);
            return View(model);
        }

        if (await _db.Customers.AnyAsync(x => x.CustomerCode == model.CustomerCode, ct))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
            await PopulateCityAreaOptionsAsync(model, ct);
            return View(model);
        }

        var entity = MapToEntity(model);
        entity.CreatedDate = DateTime.Now;
        _db.Customers.Add(entity);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Customers", entity.Uid.ToString(), $"Created customer {entity.CustomerCode}", ct);
        TempData["Success"] = "Customer created successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureCustomersNav();
        var entity = await _db.Customers.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        var model = MapToForm(entity);
        await PopulateCityAreaOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, CustomerFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        if (id != model.Uid)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            await PopulateCityAreaOptionsAsync(model, ct);
            return View(model);
        }

        var entity = await _db.Customers.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        if (await _db.Customers.AnyAsync(x => x.CustomerCode == model.CustomerCode && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
            await PopulateCityAreaOptionsAsync(model, ct);
            return View(model);
        }

        ApplyForm(entity, model);
        entity.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Customers", entity.Uid.ToString(), $"Updated customer {entity.CustomerCode}", ct);
        TempData["Success"] = "Customer updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureCustomersNav();
        var entity = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        return entity is null ? NotFound() : View(entity);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureCustomersNav();
        var entity = await _db.Customers.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        entity.IsActive = false;
        entity.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Cancel", "Customers", entity.Uid.ToString(), $"Deactivated customer {entity.CustomerCode}", ct);
        TempData["Success"] = "Customer deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private static CustomerFormViewModel MapToForm(Customer e) => new()
    {
        Uid = e.Uid,
        CustomerCode = e.CustomerCode,
        CustomerName = e.CustomerName,
        MobileNo = e.MobileNo,
        PhoneNo = e.PhoneNo,
        Email = e.Email,
        Address = e.Address,
        City = e.City,
        Area = e.Area,
        Ntn = e.Ntn,
        OpeningBalance = e.OpeningBalance,
        CreditLimit = e.CreditLimit,
        DiscountPercent = e.DiscountPercent,
        IsActive = e.IsActive
    };

    private static Customer MapToEntity(CustomerFormViewModel m) => new()
    {
        CustomerCode = m.CustomerCode.Trim(),
        CustomerName = m.CustomerName.Trim(),
        MobileNo = m.MobileNo,
        PhoneNo = m.PhoneNo,
        Email = m.Email,
        Address = m.Address,
        City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim(),
        Area = string.IsNullOrWhiteSpace(m.Area) ? null : m.Area.Trim(),
        Ntn = m.Ntn,
        OpeningBalance = m.OpeningBalance,
        CreditLimit = m.CreditLimit,
        DiscountPercent = m.DiscountPercent,
        IsActive = m.IsActive
    };

    private static void ApplyForm(Customer e, CustomerFormViewModel m)
    {
        e.CustomerCode = m.CustomerCode.Trim();
        e.CustomerName = m.CustomerName.Trim();
        e.MobileNo = m.MobileNo;
        e.PhoneNo = m.PhoneNo;
        e.Email = m.Email;
        e.Address = m.Address;
        e.City = string.IsNullOrWhiteSpace(m.City) ? null : m.City.Trim();
        e.Area = string.IsNullOrWhiteSpace(m.Area) ? null : m.Area.Trim();
        e.Ntn = m.Ntn;
        e.OpeningBalance = m.OpeningBalance;
        e.CreditLimit = m.CreditLimit;
        e.DiscountPercent = m.DiscountPercent;
        e.IsActive = m.IsActive;
    }

    private async Task PopulateCityAreaOptionsAsync(CustomerFormViewModel model, CancellationToken ct)
    {
        var configs = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == "Cities" || x.ConfigKey == "Areas")
            .Select(x => new { x.ConfigKey, x.ConfigValue })
            .ToListAsync(ct);

        var citiesRaw = configs.FirstOrDefault(x => x.ConfigKey == "Cities")?.ConfigValue;
        var areasRaw = configs.FirstOrDefault(x => x.ConfigKey == "Areas")?.ConfigValue;

        model.CityOptions = BuildConfigOptions(citiesRaw, model.City, "Select city…");
        model.AreaOptions = BuildConfigOptions(areasRaw, model.Area, "Select area…");
    }

    private static List<SelectListItem> BuildConfigOptions(string? csv, string? selected, string placeholder)
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
            new() { Value = "", Text = placeholder, Selected = string.IsNullOrWhiteSpace(selected) }
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
