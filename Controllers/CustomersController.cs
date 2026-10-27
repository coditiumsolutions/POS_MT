using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "Staff")]
public class CustomersController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly ICustomerAdvanceService _advance;

    public CustomersController(POSDbContext db, IAuditService audit, INavContextService navContext, ICustomerAdvanceService advance)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
        _advance = advance;
    }

    private void EnsureCustomersNav() => _navContext.SetArea("Customers");

    public async Task<IActionResult> Index(string? filter, string? area, CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);
        ViewData["Filter"] = filter;
        ViewData["Area"] = area;
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

        if (!string.IsNullOrWhiteSpace(area))
        {
            var areaFilter = area.Trim();
            query = query.Where(x => x.Area != null && x.Area == areaFilter);
        }

        ViewBag.AreaOptions = await BuildAreaFilterOptionsAsync(area, ct);
        var list = await query.OrderBy(x => x.CustomerName).ToListAsync(ct);
        return View(list);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureCustomersNav();
        var model = new CustomerFormViewModel { IsActive = true, GenerateCode = true };
        await ApplyGeneratedCodeAsync(model, ct);
        await PopulateCityAreaOptionsAsync(model, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomerFormViewModel model, CancellationToken ct)
    {
        EnsureCustomersNav();
        model.GenerateCode = true;
        await ApplyGeneratedCodeAsync(model, ct);
        if (!ModelState.IsValid)
        {
            await PopulateCityAreaOptionsAsync(model, ct);
            return View(model);
        }

        if (await _db.Customers.AnyAsync(x => x.CustomerCode == model.CustomerCode, ct))
        {
            await ApplyGeneratedCodeAsync(model, ct);
            if (await _db.Customers.AnyAsync(x => x.CustomerCode == model.CustomerCode, ct))
            {
                ModelState.AddModelError(nameof(model.CustomerCode), "Customer code already exists.");
                await PopulateCityAreaOptionsAsync(model, ct);
                return View(model);
            }
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
        await _advance.EnsureSchemaAsync(ct);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string? filter, string? area, CancellationToken ct)
    {
        EnsureCustomersNav();
        var entity = await _db.Customers.FindAsync([id], ct);
        if (entity is null)
        {
            return NotFound();
        }

        if (string.Equals(entity.CustomerCode, "C001", StringComparison.OrdinalIgnoreCase)
            || entity.CustomerName.Contains("Walk-in", StringComparison.OrdinalIgnoreCase)
            || entity.CustomerName.Contains("Walk-In", StringComparison.OrdinalIgnoreCase)
            || entity.CustomerName.Contains("Walk In", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = $"Cannot delete system customer {entity.CustomerCode}.";
            return RedirectToAction(nameof(Index), new { filter, area });
        }

        var code = entity.CustomerCode;
        var name = entity.CustomerName;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            await RemoveCustomerUsageAsync(id, code, ct);
            _db.Customers.Remove(entity);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Delete", "Customers", id.ToString(),
                $"Deleted customer {code} ({name}) and related records.", ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Cannot delete customer {code} because other records still use it.";
            return RedirectToAction(nameof(Index), new { filter, area });
        }

        TempData["Success"] = $"Customer {code} and related records were deleted.";
        return RedirectToAction(nameof(Index), new { filter, area });
    }

    private async Task RemoveCustomerUsageAsync(int customerId, string customerCode, CancellationToken ct)
    {
        var monthlyOverrides = await _db.CustomerMonthlySupplyOverrides
            .Where(x => x.CustomerUid == customerId)
            .ToListAsync(ct);
        if (monthlyOverrides.Count > 0)
            _db.CustomerMonthlySupplyOverrides.RemoveRange(monthlyOverrides);

        var monthly = await _db.CustomerMonthlySupplies
            .Where(x => x.CustomerUid == customerId || x.CustomerCode == customerCode)
            .ToListAsync(ct);
        if (monthly.Count > 0)
            _db.CustomerMonthlySupplies.RemoveRange(monthly);

        var payments = await _db.CustomerPayments
            .Where(x => x.CustomerUid == customerId)
            .ToListAsync(ct);
        if (payments.Count > 0)
            _db.CustomerPayments.RemoveRange(payments);

        var invoices = await _db.SalesInvoices
            .Where(x => x.CustomerUid == customerId)
            .ToListAsync(ct);
        if (invoices.Count > 0)
        {
            var invoiceIds = invoices.Select(x => x.Uid).ToList();
            var details = await _db.SalesInvoiceDetails
                .Where(x => invoiceIds.Contains(x.SalesInvoiceUid))
                .ToListAsync(ct);
            if (details.Count > 0)
                _db.SalesInvoiceDetails.RemoveRange(details);

            var inventoryTx = await _db.InventoryTransactions
                .Where(x => x.ReferenceUid != null && invoiceIds.Contains(x.ReferenceUid.Value))
                .ToListAsync(ct);
            if (inventoryTx.Count > 0)
                _db.InventoryTransactions.RemoveRange(inventoryTx);

            _db.SalesInvoices.RemoveRange(invoices);
        }

        await _db.SaveChangesAsync(ct);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult OpenItems(int[]? selectedCustomerUids, string? filter, string? area)
    {
        EnsureCustomersNav();
        if (selectedCustomerUids is null || selectedCustomerUids.Length == 0)
        {
            TempData["Error"] = "Select at least one customer.";
            return RedirectToAction(nameof(Index), new { filter, area });
        }

        var ids = string.Join(",", selectedCustomerUids.Distinct().Where(x => x > 0));
        if (string.IsNullOrWhiteSpace(ids))
        {
            TempData["Error"] = "Select at least one customer.";
            return RedirectToAction(nameof(Index), new { filter, area });
        }

        return RedirectToAction("MonthlySupplyItems", "POS", new { customerUids = ids, returnTo = "Customers" });
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

    private async Task ApplyGeneratedCodeAsync(CustomerFormViewModel model, CancellationToken ct)
    {
        model.CodePrefix = NormalizeCodePrefix(model.CodePrefix);
        ModelState.Remove(nameof(model.CodePrefix));
        var generated = await NextCustomerCodeAsync(model.CodePrefix, ct);
        model.CodeNumber = generated.Number;
        model.CustomerCode = generated.Code;
        ModelState.Remove(nameof(model.CustomerCode));
    }

    private static string NormalizeCodePrefix(string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            return string.Empty;
        }

        var cleaned = new string(prefix.Trim().Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        return cleaned.Length <= 8 ? cleaned : cleaned[..8];
    }

    /// <summary>
    /// Next code continues the highest trailing number already in use (1003 → 1004).
    /// An optional prefix is placed in front (D + 1004 → D1004). Walk-in C001 is ignored.
    /// </summary>
    private async Task<(string Number, string Code)> NextCustomerCodeAsync(string prefix, CancellationToken ct)
    {
        var codes = await _db.Customers.AsNoTracking()
            .Select(x => x.CustomerCode)
            .ToListAsync(ct);

        var max = 0;
        var width = 4;
        var any = false;
        foreach (var raw in codes)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var code = raw.Trim();
            if (string.Equals(code, "C001", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!TryTrailingNumber(code, out var number, out var digitWidth) || number <= max)
            {
                continue;
            }

            any = true;
            max = number;
            width = Math.Max(4, digitWidth);
        }

        var next = any ? max + 1 : 1000;
        var existing = new HashSet<string>(
            codes.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()),
            StringComparer.OrdinalIgnoreCase);

        string numberText;
        string candidate;
        do
        {
            numberText = next.ToString().PadLeft(width, '0');
            candidate = prefix + numberText;
            next++;
        }
        while (existing.Contains(candidate) && next < max + 100000);

        if (candidate.Length > 50)
        {
            candidate = candidate[..50];
        }

        return (numberText, candidate);
    }

    private static bool TryTrailingNumber(string code, out int number, out int width)
    {
        number = 0;
        width = 0;
        var end = code.Length;
        var start = end;
        while (start > 0 && char.IsDigit(code[start - 1]))
        {
            start--;
        }

        if (start == end)
        {
            return false;
        }

        var digits = code[start..];
        if (!int.TryParse(digits, out number))
        {
            return false;
        }

        width = digits.Length;
        return true;
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

    private async Task<List<SelectListItem>> BuildAreaFilterOptionsAsync(string? selected, CancellationToken ct)
    {
        var areasRaw = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == "Areas")
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return BuildConfigOptions(areasRaw, selected, "All areas");
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
