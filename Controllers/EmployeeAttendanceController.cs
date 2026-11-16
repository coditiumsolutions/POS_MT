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
public class EmployeeAttendanceController : Controller
{
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly IEmployeeSchemaService _schema;

    public EmployeeAttendanceController(
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

    /// <summary>View attendance by date range and branch.</summary>
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, int? branchUid, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Attendance";

        var fromDay = (fromDate ?? DateTime.Today.AddDays(-7)).Date;
        var toDay = (toDate ?? DateTime.Today).Date;
        if (toDay < fromDay)
            (fromDay, toDay) = (toDay, fromDay);

        var query =
            from a in _db.EmployeeAttendances.AsNoTracking()
            join e in _db.Employees.AsNoTracking() on a.EmployeeUid equals e.Uid
            join b in _db.Branches.AsNoTracking() on a.BranchUid equals b.Uid
            where a.AttendanceDate >= fromDay && a.AttendanceDate <= toDay
            select new { a, e, b };

        if (branchUid is int selectedBranch && selectedBranch > 0)
            query = query.Where(x => x.a.BranchUid == selectedBranch);

        var rows = await query
            .OrderByDescending(x => x.a.AttendanceDate)
            .ThenBy(x => x.b.BranchName)
            .ThenBy(x => x.e.EmployeeName)
            .Select(x => new EmployeeAttendanceListItemViewModel
            {
                Uid = x.a.Uid,
                AttendanceDate = x.a.AttendanceDate,
                EmployeeCode = x.e.EmployeeCode,
                EmployeeName = x.e.EmployeeName,
                BranchName = x.b.BranchName,
                Status = x.a.Status
            })
            .ToListAsync(ct);

        return View(new EmployeeAttendanceListPageViewModel
        {
            FromDate = fromDay,
            ToDate = toDay,
            BranchUid = branchUid is int pickedBranch && pickedBranch > 0 ? branchUid : null,
            BranchOptions = await BuildBranchOptionsAsync(branchUid, includeAll: true, ct),
            Rows = rows
        });
    }

    /// <summary>Mark attendance: employee list with checkboxes for a date and branch.</summary>
    public async Task<IActionResult> Mark(DateTime? attendanceDate, int? branchUid, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);
        ViewData["Title"] = "Mark Attendance";

        var day = (attendanceDate ?? DateTime.Today).Date;
        var dayEnd = day.AddDays(1);
        var selectedBranch = branchUid is int b && b > 0 ? branchUid : null;

        var employees = await _db.Employees.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.EmployeeName)
            .Select(x => new EmployeeAttendanceEmployeeRowViewModel
            {
                Uid = x.Uid,
                EmployeeCode = x.EmployeeCode,
                EmployeeName = x.EmployeeName,
                MobileNo1 = x.MobileNo1
            })
            .ToListAsync(ct);

        // Duplicate key = employee + calendar date (day/month/year), any branch.
        var alreadyRows = await (
            from a in _db.EmployeeAttendances.AsNoTracking()
            join br in _db.Branches.AsNoTracking() on a.BranchUid equals br.Uid into brj
            from br in brj.DefaultIfEmpty()
            where a.AttendanceDate >= day && a.AttendanceDate < dayEnd
            select new { a.EmployeeUid, BranchName = br != null ? br.BranchName : "—" }
        ).ToListAsync(ct);

        var already = alreadyRows.Select(x => x.EmployeeUid).ToHashSet();
        var alreadyBranches = alreadyRows
            .GroupBy(x => x.EmployeeUid)
            .ToDictionary(g => g.Key, g => g.First().BranchName);

        return View(new EmployeeAttendanceMarkPageViewModel
        {
            AttendanceDate = day,
            BranchUid = selectedBranch,
            BranchOptions = await BuildBranchOptionsAsync(selectedBranch, includeAll: false, ct),
            Employees = employees,
            AlreadyPresentUids = already,
            AlreadyPresentBranchNames = alreadyBranches
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Mark(EmployeeAttendanceMarkRequest model, CancellationToken ct)
    {
        EnsureNav();
        await _schema.EnsureSchemaAsync(ct);

        var day = model.AttendanceDate == default ? DateTime.Today : model.AttendanceDate.Date;
        var selected = model.SelectedEmployeeUids?.Where(x => x > 0).Distinct().ToList() ?? [];

        if (model.BranchUid <= 0)
        {
            TempData["Error"] = "Select a branch.";
            return RedirectToAction(nameof(Mark), new { attendanceDate = day.ToString("yyyy-MM-dd"), branchUid = model.BranchUid });
        }

        if (selected.Count == 0)
        {
            TempData["Error"] = "Select at least one employee.";
            return RedirectToAction(nameof(Mark), new { attendanceDate = day.ToString("yyyy-MM-dd"), branchUid = model.BranchUid });
        }

        if (!await _db.Branches.AnyAsync(x => x.Uid == model.BranchUid && x.IsActive, ct))
        {
            TempData["Error"] = "Selected branch is not available.";
            return RedirectToAction(nameof(Mark), new { attendanceDate = day.ToString("yyyy-MM-dd") });
        }

        var activeEmployees = await _db.Employees.AsNoTracking()
            .Where(x => x.IsActive && selected.Contains(x.Uid))
            .Select(x => new { x.Uid, x.EmployeeCode, x.EmployeeName })
            .ToListAsync(ct);

        if (activeEmployees.Count == 0)
        {
            TempData["Error"] = "No valid active employees selected.";
            return RedirectToAction(nameof(Mark), new { attendanceDate = day.ToString("yyyy-MM-dd"), branchUid = model.BranchUid });
        }

        var activeIds = activeEmployees.Select(x => x.Uid).ToList();
        var dayEnd = day.AddDays(1);

        // Block duplicates for the same employee code on the same day/month/year (any branch).
        var existing = await _db.EmployeeAttendances.AsNoTracking()
            .Where(x => x.AttendanceDate >= day
                        && x.AttendanceDate < dayEnd
                        && activeIds.Contains(x.EmployeeUid))
            .Select(x => x.EmployeeUid)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet();

        var duplicateCodes = activeEmployees
            .Where(x => existingSet.Contains(x.Uid))
            .Select(x => x.EmployeeCode)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
            userUid = uid;

        var now = DateTime.Now;
        var added = 0;
        foreach (var employee in activeEmployees)
        {
            if (existingSet.Contains(employee.Uid))
                continue;

            _db.EmployeeAttendances.Add(new EmployeeAttendance
            {
                EmployeeUid = employee.Uid,
                BranchUid = model.BranchUid,
                AttendanceDate = day,
                Status = "Present",
                UserUid = userUid,
                CreatedDate = now
            });
            added++;
        }

        if (added > 0)
        {
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "EmployeeAttendance", model.BranchUid.ToString(),
                $"Marked {added} present on {day:yyyy-MM-dd} for branch {model.BranchUid}", ct);
        }

        if (added == 0 && duplicateCodes.Count > 0)
        {
            TempData["Error"] =
                $"Duplicate attendance is not allowed for the same employee on the same date ({day:dd-MMM-yyyy}). " +
                $"Already marked: {string.Join(", ", duplicateCodes)}.";
        }
        else if (added == 0)
        {
            TempData["Error"] = "No attendance saved. Select employees who are not already marked for this date.";
        }
        else
        {
            TempData["Success"] = $"Marked {added} employee(s) present for {day:dd-MMM-yyyy}."
                + (duplicateCodes.Count > 0
                    ? $" Skipped duplicates: {string.Join(", ", duplicateCodes)}."
                    : "");
        }

        return RedirectToAction(nameof(Mark), new { attendanceDate = day.ToString("yyyy-MM-dd"), branchUid = model.BranchUid });
    }

    private async Task<List<SelectListItem>> BuildBranchOptionsAsync(int? selected, bool includeAll, CancellationToken ct)
    {
        var branches = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive || (selected != null && x.Uid == selected))
            .OrderBy(x => x.BranchName)
            .Select(x => new { x.Uid, x.BranchName })
            .ToListAsync(ct);

        var items = new List<SelectListItem>();
        if (includeAll)
        {
            items.Add(new SelectListItem("All branches", "", selected is null or 0));
        }
        else
        {
            items.Add(new SelectListItem("Select branch…", "", selected is null or 0));
        }

        items.AddRange(branches.Select(b => new SelectListItem(b.BranchName, b.Uid.ToString(), selected == b.Uid)));
        return items;
    }
}
