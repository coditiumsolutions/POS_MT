using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class EmployeePaymentsController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly IEmployeeSchemaService _schema;

    public EmployeePaymentsController(
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

    public async Task<IActionResult> Index(int? month, int? year, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Salary Payments";

        var periodMonth = month is >= 1 and <= 12 ? month : null;
        var periodYear = year is >= 2000 and <= 2100 ? year : null;

        var query =
            from p in _db.EmployeePayments.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on p.EmployeeUid equals e.Uid
            select new { p, e };

        if (periodMonth.HasValue)
            query = query.Where(x => x.p.PeriodMonth == periodMonth.Value);
        if (periodYear.HasValue)
            query = query.Where(x => x.p.PeriodYear == periodYear.Value);

        var rows = await query
            .OrderByDescending(x => x.p.PaymentDate)
            .ThenByDescending(x => x.p.Uid)
            .Select(x => new EmployeePaymentListItemViewModel
            {
                Uid = x.p.Uid,
                PaymentNo = x.p.PaymentNo,
                PaymentDate = x.p.PaymentDate,
                EmployeeCode = x.e.EmployeeCode,
                EmployeeName = x.e.EmployeeName,
                PeriodMonth = x.p.PeriodMonth,
                PeriodYear = x.p.PeriodYear,
                Amount = x.p.Amount,
                Remarks = x.p.Remarks
            })
            .ToListAsync(ct);

        ViewBag.Month = periodMonth;
        ViewBag.Year = periodYear;
        return View(rows);
    }

    public async Task<IActionResult> Create(int? employeeUid, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Record Salary Payment";
        await LoadEmployees(ct, employeeUid);
        var now = DateTime.Now;
        return View(new EmployeePaymentCreateViewModel
        {
            EmployeeUid = employeeUid ?? 0,
            PaymentDate = now.Date,
            PeriodMonth = now.Month,
            PeriodYear = now.Year
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeePaymentCreateViewModel model, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        await LoadEmployees(ct, model.EmployeeUid);

        if (!ModelState.IsValid)
            return View(model);

        if (!await _db.Employees.AnyAsync(x => x.Uid == model.EmployeeUid && x.IsActive, ct))
        {
            ModelState.AddModelError(nameof(model.EmployeeUid), "Select an active employee.");
            return View(model);
        }

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            userUid = uid;

        var paymentNo = $"EP-{DateTime.Now:yyyyMMddHHmmssfff}";
        var payment = new EmployeePayment
        {
            PaymentNo = paymentNo,
            EmployeeUid = model.EmployeeUid,
            PaymentDate = model.PaymentDate,
            PeriodMonth = model.PeriodMonth,
            PeriodYear = model.PeriodYear,
            Amount = Math.Round(model.Amount, 2),
            Remarks = string.IsNullOrWhiteSpace(model.Remarks) ? null : model.Remarks.Trim(),
            UserUid = userUid,
            CreatedDate = DateTime.Now
        };
        _db.EmployeePayments.Add(payment);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "EmployeePayments", payment.Uid.ToString(),
            $"Payment {paymentNo} amount {payment.Amount:N2}", ct);
        TempData["Success"] = "Salary payment recorded.";
        return RedirectToAction(nameof(Index), new { month = model.PeriodMonth, year = model.PeriodYear });
    }

    private async Task LoadEmployees(CancellationToken ct, int? selected = null)
    {
        ViewBag.Employees = new SelectList(
            await _db.Employees.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.EmployeeName)
                .Select(x => new { x.Uid, Name = x.EmployeeCode + " — " + x.EmployeeName })
                .ToListAsync(ct),
            "Uid", "Name", selected);
    }
}
