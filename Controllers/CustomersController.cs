using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public IActionResult Create()
    {
        EnsureCustomersNav();
        return View(new CustomerFormViewModel { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _db.Customers.AnyAsync(x => x.CustomerCode == model.CustomerCode, ct))
        {
            ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
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

        return View(MapToForm(entity));
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
        City = m.City,
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
        e.City = m.City;
        e.Ntn = m.Ntn;
        e.OpeningBalance = m.OpeningBalance;
        e.CreditLimit = m.CreditLimit;
        e.DiscountPercent = m.DiscountPercent;
        e.IsActive = m.IsActive;
    }
}
