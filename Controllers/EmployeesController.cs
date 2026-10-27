using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class EmployeesController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly IEmployeeSchemaService _schema;

    public EmployeesController(
        POSDbContext db,
        IAuditService audit,
        INavContextService navContext,
        IEmployeeSchemaService schema)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
        _schema = schema;
    }

    private void EnsureNav() => _navContext.SetArea("Employees");

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Employees";

        var now = DateTime.Now;
        var month = now.Month;
        var year = now.Year;

        var employees = await _db.Employees.AsNoTracking()
            .OrderBy(x => x.EmployeeName)
            .ToListAsync(ct);

        var allowances = await _db.EmployeeAllowances.AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(x => x.EmployeeUid)
            .Select(g => new { EmployeeUid = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.EmployeeUid, x => x.Total, ct);

        var deductions = await _db.EmployeeDeductions.AsNoTracking()
            .Where(x => x.IsActive)
            .GroupBy(x => x.EmployeeUid)
            .Select(g => new { EmployeeUid = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.EmployeeUid, x => x.Total, ct);

        var paid = await _db.EmployeePayments.AsNoTracking()
            .Where(x => x.PeriodMonth == month && x.PeriodYear == year)
            .GroupBy(x => x.EmployeeUid)
            .Select(g => new { EmployeeUid = g.Key, Total = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.EmployeeUid, x => x.Total, ct);

        var rows = employees.Select(e =>
        {
            var allow = allowances.GetValueOrDefault(e.Uid);
            var deduct = deductions.GetValueOrDefault(e.Uid);
            var net = e.TotalSalary + allow - deduct;
            var paidAmt = paid.GetValueOrDefault(e.Uid);
            return new EmployeeListItemViewModel
            {
                Uid = e.Uid,
                EmployeeCode = e.EmployeeCode,
                EmployeeName = e.EmployeeName,
                Cnic = e.Cnic,
                MobileNo1 = e.MobileNo1,
                TotalSalary = e.TotalSalary,
                NetSalary = net,
                PaidThisMonth = paidAmt,
                RemainingThisMonth = Math.Max(0, net - paidAmt),
                IsActive = e.IsActive
            };
        }).ToList();

        return View(rows);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Add Employee";
        var nextCode = await NextEmployeeCodeAsync(ct);
        return View(new EmployeeFormViewModel { EmployeeCode = nextCode, IsActive = true });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeFormViewModel model, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        if (!ModelState.IsValid)
            return View(model);

        if (await _db.Employees.AnyAsync(x => x.EmployeeCode == model.EmployeeCode.Trim(), ct))
        {
            ModelState.AddModelError(nameof(model.EmployeeCode), "Employee code already exists.");
            return View(model);
        }

        var e = ToEntity(model);
        e.CreatedDate = DateTime.Now;
        _db.Employees.Add(e);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Employees", e.Uid.ToString(), $"Created employee {e.EmployeeCode}", ct);
        TempData["Success"] = "Employee created.";
        return RedirectToAction(nameof(Details), new { id = e.Uid });
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        var e = await _db.Employees.FindAsync([id], ct);
        if (e is null) return NotFound();
        ViewData["Title"] = "Edit Employee";
        return View(ToForm(e));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EmployeeFormViewModel model, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        if (id != model.Uid) return BadRequest();
        if (!ModelState.IsValid) return View(model);

        var e = await _db.Employees.FindAsync([id], ct);
        if (e is null) return NotFound();

        if (await _db.Employees.AnyAsync(x => x.EmployeeCode == model.EmployeeCode.Trim() && x.Uid != id, ct))
        {
            ModelState.AddModelError(nameof(model.EmployeeCode), "Employee code already exists.");
            return View(model);
        }

        ApplyForm(e, model);
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "Employees", e.Uid.ToString(), $"Updated employee {e.EmployeeCode}", ct);
        TempData["Success"] = "Employee updated.";
        return RedirectToAction(nameof(Details), new { id = e.Uid });
    }

    public async Task<IActionResult> Details(int id, int? month, int? year, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        var e = await _db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (e is null) return NotFound();

        var periodMonth = month is >= 1 and <= 12 ? month.Value : DateTime.Now.Month;
        var periodYear = year is >= 2000 and <= 2100 ? year.Value : DateTime.Now.Year;

        var allowances = await _db.EmployeeAllowances.AsNoTracking()
            .Where(x => x.EmployeeUid == id)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Title)
            .Select(x => new EmployeeSalaryLineViewModel
            {
                Uid = x.Uid,
                Title = x.Title,
                Amount = x.Amount,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);

        var deductions = await _db.EmployeeDeductions.AsNoTracking()
            .Where(x => x.EmployeeUid == id)
            .OrderByDescending(x => x.IsActive)
            .ThenBy(x => x.Title)
            .Select(x => new EmployeeSalaryLineViewModel
            {
                Uid = x.Uid,
                Title = x.Title,
                Amount = x.Amount,
                IsActive = x.IsActive
            })
            .ToListAsync(ct);

        var payments = await _db.EmployeePayments.AsNoTracking()
            .Where(x => x.EmployeeUid == id && x.PeriodMonth == periodMonth && x.PeriodYear == periodYear)
            .OrderByDescending(x => x.PaymentDate)
            .ThenByDescending(x => x.Uid)
            .Select(x => new EmployeePaymentRowViewModel
            {
                Uid = x.Uid,
                PaymentNo = x.PaymentNo,
                PaymentDate = x.PaymentDate,
                PeriodMonth = x.PeriodMonth,
                PeriodYear = x.PeriodYear,
                Amount = x.Amount,
                Remarks = x.Remarks
            })
            .ToListAsync(ct);

        var totalAllow = allowances.Where(x => x.IsActive).Sum(x => x.Amount);
        var totalDeduct = deductions.Where(x => x.IsActive).Sum(x => x.Amount);
        var net = e.TotalSalary + totalAllow - totalDeduct;
        var paid = payments.Sum(x => x.Amount);

        ViewData["Title"] = e.EmployeeName;
        return View(new EmployeeDetailsViewModel
        {
            Employee = ToForm(e),
            Allowances = allowances,
            Deductions = deductions,
            Payments = payments,
            PeriodMonth = periodMonth,
            PeriodYear = periodYear,
            TotalSalary = e.TotalSalary,
            TotalAllowances = totalAllow,
            TotalDeductions = totalDeduct,
            NetPayable = net,
            PaidAmount = paid,
            RemainingAmount = Math.Max(0, net - paid),
            NewPaymentDate = DateTime.Today
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAllowance(int employeeUid, string title, decimal amount, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        if (employeeUid <= 0 || string.IsNullOrWhiteSpace(title) || amount <= 0)
        {
            TempData["Error"] = "Enter a valid allowance title and amount.";
            return RedirectToAction(nameof(Details), new { id = employeeUid });
        }

        _db.EmployeeAllowances.Add(new EmployeeAllowance
        {
            EmployeeUid = employeeUid,
            Title = title.Trim(),
            Amount = Math.Round(amount, 2),
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "EmployeeAllowances", employeeUid.ToString(),
            $"Added allowance '{title.Trim()}' amount {amount:N2}", ct);
        TempData["Success"] = "Allowance added.";
        return RedirectToAction(nameof(Details), new { id = employeeUid });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAllowance(int id, int employeeUid, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        var row = await _db.EmployeeAllowances.FirstOrDefaultAsync(x => x.Uid == id && x.EmployeeUid == employeeUid, ct);
        if (row is null)
        {
            TempData["Error"] = "Allowance not found.";
            return RedirectToAction(nameof(Details), new { id = employeeUid });
        }

        row.IsActive = false;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Allowance deactivated.";
        return RedirectToAction(nameof(Details), new { id = employeeUid });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDeduction(int employeeUid, string title, decimal amount, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        if (employeeUid <= 0 || string.IsNullOrWhiteSpace(title) || amount <= 0)
        {
            TempData["Error"] = "Enter a valid deduction title and amount.";
            return RedirectToAction(nameof(Details), new { id = employeeUid });
        }

        _db.EmployeeDeductions.Add(new EmployeeDeduction
        {
            EmployeeUid = employeeUid,
            Title = title.Trim(),
            Amount = Math.Round(amount, 2),
            IsActive = true,
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "EmployeeDeductions", employeeUid.ToString(),
            $"Added deduction '{title.Trim()}' amount {amount:N2}", ct);
        TempData["Success"] = "Deduction added.";
        return RedirectToAction(nameof(Details), new { id = employeeUid });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDeduction(int id, int employeeUid, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        var row = await _db.EmployeeDeductions.FirstOrDefaultAsync(x => x.Uid == id && x.EmployeeUid == employeeUid, ct);
        if (row is null)
        {
            TempData["Error"] = "Deduction not found.";
            return RedirectToAction(nameof(Details), new { id = employeeUid });
        }

        row.IsActive = false;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Deduction deactivated.";
        return RedirectToAction(nameof(Details), new { id = employeeUid });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int employeeUid, DateTime paymentDate, int periodMonth, int periodYear, decimal amount, string? remarks, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        if (employeeUid <= 0 || amount <= 0 || periodMonth is < 1 or > 12)
        {
            TempData["Error"] = "Enter a valid payment amount and period.";
            return RedirectToAction(nameof(Details), new { id = employeeUid, month = periodMonth, year = periodYear });
        }

        if (!await _db.Employees.AnyAsync(x => x.Uid == employeeUid && x.IsActive, ct))
        {
            TempData["Error"] = "Employee not found or inactive.";
            return RedirectToAction(nameof(Index));
        }

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            userUid = uid;

        var paymentNo = $"EP-{DateTime.Now:yyyyMMddHHmmssfff}";
        _db.EmployeePayments.Add(new EmployeePayment
        {
            PaymentNo = paymentNo,
            EmployeeUid = employeeUid,
            PaymentDate = paymentDate == default ? DateTime.Now : paymentDate,
            PeriodMonth = periodMonth,
            PeriodYear = periodYear,
            Amount = Math.Round(amount, 2),
            Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
            UserUid = userUid,
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "EmployeePayments", employeeUid.ToString(),
            $"Payment {paymentNo} amount {amount:N2} for {periodMonth}/{periodYear}", ct);
        TempData["Success"] = "Payment recorded.";
        return RedirectToAction(nameof(Details), new { id = employeeUid, month = periodMonth, year = periodYear });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        var e = await _db.Employees.FindAsync([id], ct);
        if (e is null) return NotFound();
        e.IsActive = false;
        e.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = "Employee deactivated.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> NextEmployeeCodeAsync(CancellationToken ct)
    {
        var codes = await _db.Employees.AsNoTracking().Select(x => x.EmployeeCode).ToListAsync(ct);
        var max = 0;
        foreach (var code in codes)
        {
            if (code.StartsWith("E", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(code.AsSpan(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
                && n > max)
                max = n;
        }

        return $"E{(max + 1).ToString("D3", CultureInfo.InvariantCulture)}";
    }

    private static EmployeeFormViewModel ToForm(Employee e) => new()
    {
        Uid = e.Uid,
        EmployeeCode = e.EmployeeCode,
        EmployeeName = e.EmployeeName,
        Cnic = e.Cnic,
        MobileNo1 = e.MobileNo1,
        MobileNo2 = e.MobileNo2,
        PresentAddress = e.PresentAddress,
        PermanentAddress = e.PermanentAddress,
        TotalSalary = e.TotalSalary,
        IsActive = e.IsActive
    };

    private static Employee ToEntity(EmployeeFormViewModel m)
    {
        var e = new Employee();
        ApplyForm(e, m);
        return e;
    }

    private static void ApplyForm(Employee e, EmployeeFormViewModel m)
    {
        e.EmployeeCode = m.EmployeeCode.Trim();
        e.EmployeeName = m.EmployeeName.Trim();
        e.Cnic = string.IsNullOrWhiteSpace(m.Cnic) ? null : m.Cnic.Trim();
        e.MobileNo1 = string.IsNullOrWhiteSpace(m.MobileNo1) ? null : m.MobileNo1.Trim();
        e.MobileNo2 = string.IsNullOrWhiteSpace(m.MobileNo2) ? null : m.MobileNo2.Trim();
        e.PresentAddress = string.IsNullOrWhiteSpace(m.PresentAddress) ? null : m.PresentAddress.Trim();
        e.PermanentAddress = string.IsNullOrWhiteSpace(m.PermanentAddress) ? null : m.PermanentAddress.Trim();
        e.TotalSalary = Math.Round(Math.Max(0, m.TotalSalary), 2);
        e.IsActive = m.IsActive;
    }
}
