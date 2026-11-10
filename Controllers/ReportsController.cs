using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.ViewModels;
using System.Globalization;

namespace POS_MT.Controllers;

[Authorize(Policy = "ManagerOrAdmin")]
public class ReportsController : Controller
{
    private readonly POSDbContext _db;
    private readonly INavContextService _navContext;

    public ReportsController(POSDbContext db, INavContextService navContext)
    {
        _db = db;
        _navContext = navContext;
    }

    private void EnsureReportsNav() => _navContext.SetArea("Reports");

    [HttpGet]
    public async Task<IActionResult> TotalSale(int? month, int? year, string? saleType, DateTime? date, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Total Sale for Monthly";

        var now = DateTime.Now;
        var selectedDate = date?.Date;
        var selectedMonth = selectedDate?.Month
            ?? (month is >= 1 and <= 12 ? month.Value : now.Month);
        var allowedYears = new[] { 2025, 2026 };
        var selectedYear = selectedDate?.Year
            ?? (year is int y && allowedYears.Contains(y) ? y
                : (allowedYears.Contains(now.Year) ? now.Year : 2026));
        var selectedSaleType = NormalizeSaleType(saleType);

        DateTime start;
        DateTime end;
        if (selectedDate.HasValue)
        {
            start = selectedDate.Value;
            end = start.AddDays(1);
        }
        else
        {
            start = new DateTime(selectedYear, selectedMonth, 1);
            end = start.AddMonths(1);
        }

        var query =
            from inv in _db.SalesInvoices.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            where inv.InvoiceDate >= start
                  && inv.InvoiceDate < end
                  && inv.InvoiceStatus != "Cancelled"
            select new { inv, c };

        if (selectedSaleType is "Cash" or "Walk-in")
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (selectedSaleType == "Monthly")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In"));
        }

        var rows = await query
            .OrderByDescending(x => x.inv.InvoiceDate)
            .ThenByDescending(x => x.inv.Uid)
            .Select(x => new TotalSaleReportRowViewModel
            {
                Uid = x.inv.Uid,
                InvoiceNo = x.inv.InvoiceNo,
                InvoiceDate = x.inv.InvoiceDate,
                CustomerName = x.c != null ? x.c.CustomerName : "—",
                SaleType = x.inv.InvoiceStatus == SalesReturnsController.ReturnStatus
                    ? "Return"
                    : (x.inv.InvoiceStatus == WastageController.WastageStatus
                        ? "Wastage"
                        : (x.c == null
                            ? "—"
                            : (x.c.CustomerCode == "C001" ||
                               x.c.CustomerName.Contains("Walk-in") ||
                               x.c.CustomerName.Contains("Walk-In") ||
                               x.c.CustomerName.Contains("Walk In")
                                ? "Walk-in"
                                : "Monthly"))),
                NetAmount = x.inv.NetAmount,
                PaidAmount = x.inv.PaidAmount,
                InvoiceStatus = x.inv.InvoiceStatus,
                PaymentStatus = x.inv.PaymentStatus
            })
            .ToListAsync(ct);

        var model = new TotalSaleReportViewModel
        {
            Month = selectedMonth,
            Year = selectedYear,
            Date = selectedDate,
            SaleType = selectedSaleType,
            Months = BuildMonthOptions(selectedMonth),
            Years = BuildYearOptions(selectedYear),
            SaleTypes = BuildSaleTypeOptions(selectedSaleType),
            InvoiceCount = rows.Count,
            TotalNetAmount = rows.Sum(x => x.NetAmount),
            TotalPaidAmount = rows.Sum(x => x.PaidAmount),
            TotalBalanceAmount = rows.Sum(x => x.NetAmount - x.PaidAmount),
            Rows = rows
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> TerminalReport(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? branch,
        string? terminal,
        string? user,
        CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Terminal Report";

        var today = DateTime.Today;
        var startDate = (dateFrom ?? today).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < startDate)
            (startDate, toInclusive) = (toInclusive, startDate);
        var toExclusive = toInclusive.AddDays(1);

        var selectedBranch = string.IsNullOrWhiteSpace(branch) || branch == "All" ? null : branch.Trim();
        var selectedTerminal = string.IsNullOrWhiteSpace(terminal) || terminal == "All" ? null : terminal.Trim();
        var selectedUser = string.IsNullOrWhiteSpace(user) || user == "All" ? null : user.Trim();

        var branchNames = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive)
            .OrderBy(b => b.BranchName)
            .Select(b => b.BranchName)
            .ToListAsync(ct);
        var terminalNames = await _db.Terminals.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.TerminalName)
            .Select(t => t.TerminalName)
            .ToListAsync(ct);
        var userNames = await _db.Users.AsNoTracking()
            .Where(u => u.IsActive)
            .OrderBy(u => u.UserName)
            .Select(u => u.UserName)
            .ToListAsync(ct);

        var query =
            from i in _db.SalesInvoices.AsNoTracking()
            join u in _db.Users.AsNoTracking() on i.UserUid equals u.Uid into uj
            from u in uj.DefaultIfEmpty()
            where i.InvoiceDate >= startDate
                  && i.InvoiceDate < toExclusive
                  && i.InvoiceStatus != "Cancelled"
            select new
            {
                i.Branch,
                i.Terminal,
                UserName = !string.IsNullOrWhiteSpace(i.UserName)
                    ? i.UserName
                    : (u != null ? u.UserName : null),
                i.NetAmount,
                i.PaidAmount
            };

        if (selectedBranch is not null)
            query = query.Where(i => i.Branch == selectedBranch);

        if (selectedTerminal is not null)
            query = query.Where(i => i.Terminal == selectedTerminal);

        if (selectedUser is not null)
            query = query.Where(i => i.UserName == selectedUser);

        var rows = await query
            .GroupBy(i => new
            {
                Branch = string.IsNullOrWhiteSpace(i.Branch) ? "Unassigned" : i.Branch!.Trim(),
                Terminal = string.IsNullOrWhiteSpace(i.Terminal) ? "Unassigned" : i.Terminal!.Trim(),
                UserName = string.IsNullOrWhiteSpace(i.UserName) ? "Unassigned" : i.UserName!.Trim()
            })
            .Select(g => new TerminalDailyReportRowViewModel
            {
                BranchName = g.Key.Branch,
                TerminalName = g.Key.Terminal,
                UserName = g.Key.UserName,
                InvoiceCount = g.Count(),
                TotalNetAmount = g.Sum(x => x.NetAmount),
                TotalPaidAmount = g.Sum(x => x.PaidAmount)
            })
            .OrderBy(x => x.BranchName)
            .ThenBy(x => x.TerminalName)
            .ThenBy(x => x.UserName)
            .ToListAsync(ct);

        return View(new TerminalDailyReportViewModel
        {
            DateFrom = startDate,
            DateTo = toInclusive,
            Branch = selectedBranch ?? "All",
            Terminal = selectedTerminal ?? "All",
            UserName = selectedUser ?? "All",
            Branches = BuildAllOptionList(branchNames, selectedBranch),
            Terminals = BuildAllOptionList(terminalNames, selectedTerminal),
            Users = BuildAllOptionList(userNames, selectedUser),
            Rows = rows,
            TotalInvoices = rows.Sum(x => x.InvoiceCount),
            TotalNetAmount = rows.Sum(x => x.TotalNetAmount),
            TotalPaidAmount = rows.Sum(x => x.TotalPaidAmount)
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExpenseReport(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Financial Report";

        var today = DateTime.Today;
        var from = (dateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < from)
        {
            (from, toInclusive) = (toInclusive, from);
        }

        var toExclusive = toInclusive.AddDays(1);

        var expenseQuery = _db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate < toExclusive);
        var expenseCount = await expenseQuery.CountAsync(ct);
        var expenseTotal = await expenseQuery.SumAsync(e => (decimal?)e.Amount, ct) ?? 0m;

        var employeePayQuery = _db.EmployeePayments.AsNoTracking()
            .Where(p => p.PaymentDate >= from && p.PaymentDate < toExclusive);
        var employeePaymentCount = await employeePayQuery.CountAsync(ct);
        var employeePaymentTotal = await employeePayQuery.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var vendorPayQuery = _db.VendorPayments.AsNoTracking()
            .Where(p => p.PaymentDate >= from && p.PaymentDate < toExclusive);
        var vendorPaymentCount = await vendorPayQuery.CountAsync(ct);
        var vendorPaymentTotal = await vendorPayQuery.SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var invoiceQuery = _db.SalesInvoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= from
                        && i.InvoiceDate < toExclusive
                        && i.InvoiceStatus != "Cancelled");
        var customerInvoiceCount = await invoiceQuery.CountAsync(ct);
        var customerInvoiceTotal = await invoiceQuery.SumAsync(i => (decimal?)i.NetAmount, ct) ?? 0m;

        return View(new ExpenseReportViewModel
        {
            DateFrom = from,
            DateTo = toInclusive,
            ExpenseCount = expenseCount,
            ExpenseTotal = expenseTotal,
            EmployeePaymentCount = employeePaymentCount,
            EmployeePaymentTotal = employeePaymentTotal,
            VendorPaymentCount = vendorPaymentCount,
            VendorPaymentTotal = vendorPaymentTotal,
            CustomerInvoiceCount = customerInvoiceCount,
            CustomerInvoiceTotal = customerInvoiceTotal
        });
    }

    [HttpGet]
    public async Task<IActionResult> PaymentReport(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Payment Report";
        var model = await BuildPaymentReportAsync(dateFrom, dateTo, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PrintPaymentReport(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Payment Report";
        var model = await BuildPaymentReportAsync(dateFrom, dateTo, ct);
        model.PrintedAt = DateTime.Now;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> VendorPaymentReport(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Vendor Payments";
        var model = await BuildVendorPaymentReportAsync(dateFrom, dateTo, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PrintVendorPaymentReport(DateTime? dateFrom, DateTime? dateTo, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Vendor Payments";
        var model = await BuildVendorPaymentReportAsync(dateFrom, dateTo, ct);
        model.PrintedAt = DateTime.Now;
        return View(model);
    }

    private async Task<PaymentReportViewModel> BuildPaymentReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken ct)
    {
        var today = DateTime.Today;
        var startDate = (dateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < startDate)
            (startDate, toInclusive) = (toInclusive, startDate);
        var toExclusive = toInclusive.AddDays(1);

        var rows = await (
            from p in _db.CustomerPayments.AsNoTracking()
            join c in _db.Customers.AsNoTracking() on p.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            join inv in _db.SalesInvoices.AsNoTracking() on p.SalesInvoiceUid equals inv.Uid into ij
            from inv in ij.DefaultIfEmpty()
            join pm in _db.PaymentMethods.AsNoTracking() on p.PaymentMethodUid equals pm.Uid into pmj
            from pm in pmj.DefaultIfEmpty()
            where p.PaymentDate >= startDate && p.PaymentDate < toExclusive
            orderby p.PaymentDate, p.Uid
            select new PaymentReportRowViewModel
            {
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                CustomerName = c != null ? c.CustomerName : ("#" + p.CustomerUid),
                PaymentType = p.SalesInvoiceUid == null || p.AppliedToInvoiceAmount <= 0
                    ? "Advance"
                    : (p.Amount > p.AppliedToInvoiceAmount ? "Invoice + Advance" : "Against Invoice"),
                InvoiceNo = inv != null ? inv.InvoiceNo : null,
                PaymentMethod = pm != null ? pm.PaymentName : null,
                ReferenceNo = p.ReferenceNo,
                Amount = p.Amount,
                AppliedToInvoiceAmount = p.AppliedToInvoiceAmount,
                Remarks = p.Remarks
            }).ToListAsync(ct);

        return new PaymentReportViewModel
        {
            DateFrom = startDate,
            DateTo = toInclusive,
            Rows = rows,
            PaymentCount = rows.Count,
            TotalAmount = rows.Sum(x => x.Amount),
            TotalAppliedToInvoice = rows.Sum(x => x.AppliedToInvoiceAmount),
            TotalAdvance = rows.Sum(x => x.AdvanceAmount),
            PrintedAt = DateTime.Now
        };
    }

    private async Task<VendorPaymentReportViewModel> BuildVendorPaymentReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken ct)
    {
        var today = DateTime.Today;
        var startDate = (dateFrom ?? new DateTime(today.Year, today.Month, 1)).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < startDate)
            (startDate, toInclusive) = (toInclusive, startDate);
        var toExclusive = toInclusive.AddDays(1);

        var rows = await (
            from p in _db.VendorPayments.AsNoTracking()
            join v in _db.Vendors.AsNoTracking() on p.VendorUid equals v.Uid into vj
            from v in vj.DefaultIfEmpty()
            join inv in _db.PurchaseInvoices.AsNoTracking() on p.PurchaseInvoiceUid equals inv.Uid into ij
            from inv in ij.DefaultIfEmpty()
            join pm in _db.PaymentMethods.AsNoTracking() on p.PaymentMethodUid equals pm.Uid into pmj
            from pm in pmj.DefaultIfEmpty()
            where p.PaymentDate >= startDate && p.PaymentDate < toExclusive
            orderby p.PaymentDate, p.Uid
            select new VendorPaymentReportRowViewModel
            {
                PaymentNo = p.PaymentNo,
                PaymentDate = p.PaymentDate,
                VendorName = v != null ? v.VendorName : ("#" + p.VendorUid),
                PurchaseInvoiceNo = inv != null ? inv.PurchaseInvoiceNo : null,
                PaymentMethod = pm != null ? pm.PaymentName : null,
                ReferenceNo = p.ReferenceNo,
                Amount = p.Amount,
                Remarks = p.Remarks
            }).ToListAsync(ct);

        return new VendorPaymentReportViewModel
        {
            DateFrom = startDate,
            DateTo = toInclusive,
            Rows = rows,
            PaymentCount = rows.Count,
            TotalAmount = rows.Sum(x => x.Amount),
            PrintedAt = DateTime.Now
        };
    }

    [HttpGet]
    public async Task<IActionResult> SalesItem(DateTime? dateFrom, DateTime? dateTo, string? saleType, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Sales Item";

        var today = DateTime.Today;
        var startDate = (dateFrom ?? today).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < startDate)
        {
            (startDate, toInclusive) = (toInclusive, startDate);
        }

        var endExclusive = toInclusive.AddDays(1);
        var selectedSaleType = NormalizeSalesItemSaleType(saleType);

        var query =
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            where inv.InvoiceDate >= startDate
                  && inv.InvoiceDate < endExclusive
                  && inv.InvoiceStatus != "Cancelled"
                  && inv.InvoiceStatus != SalesReturnsController.ReturnStatus
                  && inv.InvoiceStatus != WastageController.WastageStatus
            select new { d, inv, c };

        if (selectedSaleType is "Cash" or "Walk-in")
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (selectedSaleType == "Monthly")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                x.inv.Remarks != null &&
                x.inv.Remarks.StartsWith("Monthly Supply"));
        }
        else if (selectedSaleType == "Credit")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                (x.inv.Remarks == null || !x.inv.Remarks.StartsWith("Monthly Supply")));
        }

        var rows = await query
            .OrderByDescending(x => x.inv.InvoiceDate)
            .ThenByDescending(x => x.inv.Uid)
            .ThenBy(x => x.d.Uid)
            .Select(x => new SalesItemReportRowViewModel
            {
                InvoiceDate = x.inv.InvoiceDate,
                InvoiceNo = x.inv.InvoiceNo,
                SalesInvoiceUid = x.inv.Uid,
                CustomerName = x.c != null ? x.c.CustomerName : "—",
                SaleType = x.c == null
                    ? "—"
                    : (x.c.CustomerCode == "C001" ||
                       x.c.CustomerName.Contains("Walk-in") ||
                       x.c.CustomerName.Contains("Walk-In") ||
                       x.c.CustomerName.Contains("Walk In")
                        ? "Walk-in"
                        : (x.inv.Remarks != null && x.inv.Remarks.StartsWith("Monthly Supply")
                            ? "Monthly"
                            : "Credit")),
                ProductCode = x.d.ProductCode ?? string.Empty,
                ProductName = x.d.ProductName ?? x.d.ProductCode ?? "—",
                Quantity = x.d.Quantity,
                UnitPrice = x.d.UnitPrice,
                LineTotal = x.d.LineTotal
            })
            .ToListAsync(ct);

        return View(new SalesItemReportViewModel
        {
            DateFrom = startDate,
            DateTo = toInclusive,
            SaleType = selectedSaleType,
            SaleTypes = BuildSalesItemSaleTypeOptions(selectedSaleType),
            Rows = rows
        });
    }

    [HttpGet]
    public async Task<IActionResult> InventoryReport(DateTime? dateFrom, DateTime? dateTo, string? saleType, string? itemSearch, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Inventory Report";
        var model = await BuildInventoryReportAsync(dateFrom, dateTo, saleType, itemSearch, ct);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> PrintInventoryReport(DateTime? dateFrom, DateTime? dateTo, string? saleType, string? itemSearch, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Inventory Report";
        var model = await BuildInventoryReportAsync(dateFrom, dateTo, saleType, itemSearch, ct);
        model.PrintedAt = DateTime.Now;
        return View(model);
    }

    private async Task<InventoryReportViewModel> BuildInventoryReportAsync(
        DateTime? dateFrom,
        DateTime? dateTo,
        string? saleType,
        string? itemSearch,
        CancellationToken ct)
    {
        var today = DateTime.Today;
        var startDate = (dateFrom ?? today).Date;
        var toInclusive = (dateTo ?? today).Date;
        if (toInclusive < startDate)
            (startDate, toInclusive) = (toInclusive, startDate);

        var endExclusive = toInclusive.AddDays(1);
        var selectedSaleType = NormalizeSalesItemSaleType(saleType);
        var search = string.IsNullOrWhiteSpace(itemSearch) ? string.Empty : itemSearch.Trim();

        var query =
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            join p in _db.Products.AsNoTracking() on d.ProductUid equals p.Uid into pj
            from p in pj.DefaultIfEmpty()
            join u in _db.ProductUnits.AsNoTracking() on p.UnitUid equals u.Uid into uj
            from u in uj.DefaultIfEmpty()
            where inv.InvoiceDate >= startDate
                  && inv.InvoiceDate < endExclusive
                  && inv.InvoiceStatus != "Cancelled"
                  && inv.InvoiceStatus != SalesReturnsController.ReturnStatus
                  && inv.InvoiceStatus != WastageController.WastageStatus
            select new { d, inv, c, p, u };

        if (selectedSaleType is "Cash" or "Walk-in")
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (selectedSaleType == "Monthly")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                x.inv.Remarks != null &&
                x.inv.Remarks.StartsWith("Monthly Supply"));
        }
        else if (selectedSaleType == "Credit")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                (x.inv.Remarks == null || !x.inv.Remarks.StartsWith("Monthly Supply")));
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(x =>
                (x.d.ProductName != null && x.d.ProductName.Contains(search)) ||
                (x.p != null && x.p.ProductName.Contains(search)));
        }

        var rows = await query
            .GroupBy(x => new
            {
                ProductUid = x.d.ProductUid,
                ProductCode = x.d.ProductCode ?? (x.p != null ? x.p.ProductCode : string.Empty),
                ProductName = x.d.ProductName ?? (x.p != null ? x.p.ProductName : null) ?? x.d.ProductCode ?? "—",
                Unit = x.u != null
                    ? (!string.IsNullOrWhiteSpace(x.u.UnitCode) ? x.u.UnitCode : x.u.UnitName)
                    : "—"
            })
            .Select(g => new InventoryReportRowViewModel
            {
                ProductCode = g.Key.ProductCode ?? string.Empty,
                ProductName = g.Key.ProductName,
                Unit = g.Key.Unit ?? "—",
                QuantitySold = g.Sum(x => x.d.Quantity),
                Amount = g.Sum(x => x.d.LineTotal)
            })
            .OrderBy(x => x.ProductName)
            .ThenBy(x => x.ProductCode)
            .ToListAsync(ct);

        return new InventoryReportViewModel
        {
            DateFrom = startDate,
            DateTo = toInclusive,
            SaleType = selectedSaleType,
            ItemSearch = search,
            SaleTypes = BuildSalesItemSaleTypeOptions(selectedSaleType),
            Rows = rows,
            PrintedAt = DateTime.Now
        };
    }

    [HttpGet]
    public async Task<IActionResult> DailyReport(DateTime? date, string? saleType, CancellationToken ct)
    {
        EnsureReportsNav();
        ViewData["Title"] = "Daily Report";

        var selectedDate = (date ?? DateTime.Today).Date;
        var selectedSaleType = NormalizeSalesItemSaleType(saleType);
        var dayStart = selectedDate;
        var dayEnd = dayStart.AddDays(1);

        var query =
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            join c in _db.Customers.AsNoTracking() on inv.CustomerUid equals c.Uid into cj
            from c in cj.DefaultIfEmpty()
            where inv.InvoiceDate >= dayStart
                  && inv.InvoiceDate < dayEnd
                  && inv.InvoiceStatus != "Cancelled"
            select new { d, inv, c };

        if (selectedSaleType is "Cash" or "Walk-in")
        {
            query = query.Where(x => x.c != null &&
                (x.c.CustomerCode == "C001" ||
                 x.c.CustomerName.Contains("Walk-in") ||
                 x.c.CustomerName.Contains("Walk-In") ||
                 x.c.CustomerName.Contains("Walk In")));
        }
        else if (selectedSaleType == "Monthly")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                x.inv.Remarks != null &&
                x.inv.Remarks.StartsWith("Monthly Supply"));
        }
        else if (selectedSaleType == "Credit")
        {
            query = query.Where(x => x.c != null &&
                x.c.CustomerCode != "C001" &&
                !x.c.CustomerName.Contains("Walk-in") &&
                !x.c.CustomerName.Contains("Walk-In") &&
                !x.c.CustomerName.Contains("Walk In") &&
                (x.inv.Remarks == null || !x.inv.Remarks.StartsWith("Monthly Supply")));
        }

        var raw = await query
            .OrderBy(x => x.inv.InvoiceDate)
            .ThenBy(x => x.inv.Uid)
            .ThenBy(x => x.d.Uid)
            .Select(x => new
            {
                x.inv.Uid,
                x.inv.InvoiceDate,
                Item = x.d.ProductName ?? x.d.ProductCode ?? "—",
                x.d.Quantity,
                Amount = x.d.LineTotal,
                DeliveryCharges = x.inv.OtherCharges
            })
            .ToListAsync(ct);

        var seenInvoices = new HashSet<int>();
        var rows = raw.Select(x =>
        {
            var showDelivery = seenInvoices.Add(x.Uid);
            return new DailyReportRowViewModel
            {
                Date = x.InvoiceDate,
                Item = x.Item,
                Quantity = x.Quantity,
                Amount = x.Amount,
                DeliveryCharges = showDelivery ? x.DeliveryCharges : 0m
            };
        }).ToList();

        return View(new DailyReportViewModel
        {
            Date = selectedDate,
            SaleType = selectedSaleType,
            SaleTypes = BuildSalesItemSaleTypeOptions(selectedSaleType),
            Rows = rows,
            TotalQuantity = rows.Sum(x => x.Quantity),
            TotalAmount = rows.Sum(x => x.Amount),
            TotalDeliveryCharges = rows.Sum(x => x.DeliveryCharges)
        });
    }

    private static IEnumerable<SelectListItem> BuildMonthOptions(int selectedMonth) =>
        Enumerable.Range(1, 12)
            .Select(m => new SelectListItem
            {
                Value = m.ToString(CultureInfo.InvariantCulture),
                Text = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(m),
                Selected = m == selectedMonth
            });

    private static IEnumerable<SelectListItem> BuildYearOptions(int selectedYear) =>
    [
        new SelectListItem
        {
            Value = "2025",
            Text = "2025",
            Selected = selectedYear == 2025
        },
        new SelectListItem
        {
            Value = "2026",
            Text = "2026",
            Selected = selectedYear == 2026
        }
    ];

    private static IEnumerable<SelectListItem> BuildSaleTypeOptions(string selectedSaleType) =>
    [
        new SelectListItem { Value = "All", Text = "All", Selected = selectedSaleType == "All" },
        new SelectListItem { Value = "Monthly", Text = "Monthly", Selected = selectedSaleType == "Monthly" },
        new SelectListItem { Value = "Cash", Text = "Cash", Selected = selectedSaleType == "Cash" },
        new SelectListItem { Value = "Walk-in", Text = "Walk-in", Selected = selectedSaleType == "Walk-in" }
    ];

    private static IEnumerable<SelectListItem> BuildSalesItemSaleTypeOptions(string selectedSaleType) =>
    [
        new SelectListItem { Value = "All", Text = "All", Selected = selectedSaleType == "All" },
        new SelectListItem { Value = "Walk-in", Text = "Walk-in", Selected = selectedSaleType == "Walk-in" },
        new SelectListItem { Value = "Monthly", Text = "Monthly", Selected = selectedSaleType == "Monthly" },
        new SelectListItem { Value = "Credit", Text = "Credit", Selected = selectedSaleType == "Credit" }
    ];

    private static IEnumerable<SelectListItem> BuildAllOptionList(IEnumerable<string> values, string? selected)
    {
        var selectedValue = string.IsNullOrWhiteSpace(selected) ? "All" : selected.Trim();
        yield return new SelectListItem { Value = "All", Text = "All", Selected = selectedValue == "All" };
        foreach (var value in values)
        {
            yield return new SelectListItem
            {
                Value = value,
                Text = value,
                Selected = string.Equals(value, selectedValue, StringComparison.OrdinalIgnoreCase)
            };
        }
    }

    private static string NormalizeSaleType(string? saleType)
    {
        if (string.Equals(saleType, "Cash", StringComparison.OrdinalIgnoreCase))
        {
            return "Cash";
        }

        if (string.Equals(saleType, "Walk-in", StringComparison.OrdinalIgnoreCase)
            || string.Equals(saleType, "Walkin", StringComparison.OrdinalIgnoreCase))
        {
            return "Walk-in";
        }

        if (string.Equals(saleType, "All", StringComparison.OrdinalIgnoreCase))
        {
            return "All";
        }

        return "Monthly";
    }

    private static string NormalizeSalesItemSaleType(string? saleType)
    {
        if (string.Equals(saleType, "Cash", StringComparison.OrdinalIgnoreCase)
            || string.Equals(saleType, "Walk-in", StringComparison.OrdinalIgnoreCase)
            || string.Equals(saleType, "Walkin", StringComparison.OrdinalIgnoreCase))
        {
            return "Walk-in";
        }

        if (string.Equals(saleType, "Credit", StringComparison.OrdinalIgnoreCase))
        {
            return "Credit";
        }

        if (string.Equals(saleType, "Monthly", StringComparison.OrdinalIgnoreCase))
        {
            return "Monthly";
        }

        return "All";
    }
}
