using System.Globalization;
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

[Authorize(Policy = "Staff")]
public class CustomersController : Controller
{
    private const string MonthlySupplyRemarkPrefix = "Monthly Supply";
    private const string MonthlyEntryDatePrefix = "EntryDate:";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly INavContextService _navContext;
    private readonly ICustomerAdvanceService _advance;
    private readonly IInventoryLedgerService _inventory;

    public CustomersController(
        POSDbContext db,
        IAuditService audit,
        INavContextService navContext,
        ICustomerAdvanceService advance,
        IInventoryLedgerService inventory)
    {
        _db = db;
        _audit = audit;
        _navContext = navContext;
        _advance = advance;
        _inventory = inventory;
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
        if (entity is null)
            return NotFound();

        // Monthly Supply is lump-sum at month end — clear any mid-month auto-advance "Paid" state.
        await _advance.ReverseAutoAppliedAdvancesOnMonthlySupplyAsync(id, ct);
        await _db.SaveChangesAsync(ct);

        await PopulateCustomerDetailsExtrasAsync(id, ct);
        return View(entity);
    }

    /// <summary>
    /// Deletes one Current Month Bills row: a Monthly Supply day (EntryDate lines) or a whole non-monthly invoice.
    /// Restores stock and recalculates remaining monthly totals.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCurrentMonthBill(
        int id,
        int invoiceUid,
        DateTime billDate,
        bool isMonthlyDayBill,
        CancellationToken ct)
    {
        EnsureCustomersNav();
        TempData["OpenTab"] = "current-bills";

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (customer is null)
            return NotFound();

        var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(
            x => x.Uid == invoiceUid && x.CustomerUid == id, ct);
        if (invoice is null)
        {
            TempData["Error"] = "Bill was not found for this customer.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Cancelled shells (empty / leftover monthly invoices) can be removed permanently from the list.
        if (string.Equals(invoice.InvoiceStatus, "Cancelled", StringComparison.OrdinalIgnoreCase))
        {
            await using var cancelTx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                await PurgeSalesInvoiceAsync(invoice.Uid, ct);
                await _audit.WriteAsync("Delete", "SalesInvoices", invoice.Uid.ToString(),
                    $"Permanently deleted cancelled invoice {invoice.InvoiceNo} from customer bills.", ct);
                await cancelTx.CommitAsync(ct);
                TempData["Success"] = $"Deleted cancelled bill {invoice.InvoiceNo}.";
            }
            catch (Exception ex)
            {
                await cancelTx.RollbackAsync(ct);
                TempData["Error"] = "Could not delete cancelled bill: " + (ex.InnerException?.Message ?? ex.Message);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        int? userUid = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var parsedUserId))
            userUid = parsedUserId;

        var day = billDate.Date;
        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (isMonthlyDayBill
                || CustomerClassification.IsMonthlySupplyInvoice(invoice.Remarks))
            {
                var entryRemark = $"{MonthlyEntryDatePrefix}{day:yyyy-MM-dd}";
                var dayLines = await _db.SalesInvoiceDetails
                    .Where(x => x.SalesInvoiceUid == invoice.Uid
                                && x.Remarks != null
                                && x.Remarks == entryRemark)
                    .ToListAsync(ct);

                if (dayLines.Count == 0)
                {
                    TempData["Error"] = $"No supply lines found for {day:dd MMM yyyy}.";
                    await tx.RollbackAsync(ct);
                    return RedirectToAction(nameof(Details), new { id });
                }

                foreach (var detail in dayLines)
                {
                    if (detail.Quantity > 0 && detail.ProductUid > 0)
                    {
                        await _inventory.ApplySaleQuantityDeltaAsync(
                            invoice.Uid,
                            invoice.InvoiceNo,
                            new InventoryLineMovement
                            {
                                ProductUid = detail.ProductUid,
                                ProductName = detail.ProductName ?? detail.ProductUid.ToString(),
                                Quantity = -detail.Quantity,
                                UnitCost = detail.UnitPrice
                            },
                            warehouseUid: null,
                            invoice.BranchUid,
                            userUid ?? invoice.UserUid,
                            terminalUid: null,
                            ct);
                    }
                }

                _db.SalesInvoiceDetails.RemoveRange(dayLines);
                await _db.SaveChangesAsync(ct);

                // SQLite cannot Sum(decimal) in SQL — aggregate on the client.
                var remainingTotal = (await _db.SalesInvoiceDetails
                    .Where(x => x.SalesInvoiceUid == invoice.Uid)
                    .Select(x => x.LineTotal)
                    .ToListAsync(ct)).Sum();
                var remainingCount = await _db.SalesInvoiceDetails
                    .CountAsync(x => x.SalesInvoiceUid == invoice.Uid, ct);

                if (remainingCount == 0)
                {
                    invoice.InvoiceStatus = "Cancelled";
                    invoice.PaymentStatus = "Cancelled";
                    invoice.SubTotal = 0;
                    invoice.NetAmount = 0;
                    invoice.BalanceAmount = 0;
                    invoice.OtherCharges = 0;
                    invoice.PaidAmount = 0;
                }
                else
                {
                    invoice.SubTotal = remainingTotal;
                    invoice.NetAmount = remainingTotal + Math.Max(0, invoice.OtherCharges);
                    invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
                    invoice.PaymentStatus = invoice.PaidAmount <= 0
                        ? "Unpaid"
                        : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
                }

                invoice.UpdatedDate = DateTime.Now;
                await _db.SaveChangesAsync(ct);
                await _advance.ReverseAutoAppliedAdvancesOnMonthlySupplyAsync(id, ct);
                await _db.SaveChangesAsync(ct);

                await _audit.WriteAsync("Delete", "SalesInvoiceDetails", invoice.Uid.ToString(),
                    $"Deleted current-month supply for {customer.CustomerCode} on {day:yyyy-MM-dd} ({dayLines.Count} line(s), stock restored).", ct);
                await tx.CommitAsync(ct);

                TempData["Success"] = $"Deleted supply for {day:dd MMM yyyy}. Stock restored.";
                return RedirectToAction(nameof(Details), new { id });
            }

            // Non-monthly invoice row: cancel whole invoice and restore stock.
            invoice.InvoiceStatus = "Cancelled";
            invoice.UpdatedDate = DateTime.Now;
            await _inventory.ReverseSaleAsync(invoice.Uid, invoice.InvoiceNo, userUid ?? invoice.UserUid, invoice.TerminalUid, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Cancel", "SalesInvoices", invoice.Uid.ToString(),
                $"Cancelled invoice {invoice.InvoiceNo} from customer current-month bills.", ct);
            await tx.CommitAsync(ct);

            TempData["Success"] = $"Deleted bill {invoice.InvoiceNo}. Stock restored.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = "Delete failed: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>Permanently removes a sales invoice and its details / linked payments / inventory tx.</summary>
    private async Task PurgeSalesInvoiceAsync(int invoiceUid, CancellationToken ct)
    {
        var details = await _db.SalesInvoiceDetails
            .Where(x => x.SalesInvoiceUid == invoiceUid)
            .ToListAsync(ct);
        if (details.Count > 0)
            _db.SalesInvoiceDetails.RemoveRange(details);

        var payments = await _db.CustomerPayments
            .Where(x => x.SalesInvoiceUid == invoiceUid)
            .ToListAsync(ct);
        if (payments.Count > 0)
            _db.CustomerPayments.RemoveRange(payments);

        var inventoryTx = await _db.InventoryTransactions
            .Where(x => x.ReferenceUid == invoiceUid)
            .ToListAsync(ct);
        if (inventoryTx.Count > 0)
            _db.InventoryTransactions.RemoveRange(inventoryTx);

        var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x => x.Uid == invoiceUid, ct);
        if (invoice is not null)
            _db.SalesInvoices.Remove(invoice);

        await _db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Posts an unpaid header-only sales invoice for prior-month / opening arrears.
    /// Shows as Previous Balance on later monthly bills; payments clear it via FIFO.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateOpeningBalanceInvoice(
        int id,
        decimal amount,
        DateTime? asOfDate,
        string? note,
        CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);

        var customer = await _db.Customers.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (customer is null)
            return NotFound();

        if (amount <= 0)
        {
            TempData["Error"] = "Opening / previous balance amount must be greater than zero.";
            TempData["OpenTab"] = "arrears";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (CustomerClassification.IsWalkIn(customer.CustomerCode, customer.CustomerName))
        {
            TempData["Error"] = "Cannot post previous balance for the walk-in customer.";
            TempData["OpenTab"] = "arrears";
            return RedirectToAction(nameof(Details), new { id });
        }

        var invoiceDate = (asOfDate ?? DateTime.Today).Date;
        var notePart = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var remarks = notePart is null
            ? $"{CustomerClassification.OpeningBalanceRemarkPrefix} as of {invoiceDate:yyyy-MM-dd}"
            : $"{CustomerClassification.OpeningBalanceRemarkPrefix} as of {invoiceDate:yyyy-MM-dd}: {notePart}";
        if (remarks.Length > 500)
            remarks = remarks[..500];

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
            userUid = parsedUserId;
        var userName = User.Identity?.Name;

        var invoiceNo = $"OB-{DateTime.Now:yyyyMMddHHmmss}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
            invoiceNo = $"OB-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(10, 99)}";

        var invoice = new SalesInvoice
        {
            InvoiceNo = invoiceNo,
            InvoiceDate = invoiceDate,
            CustomerUid = customer.Uid,
            UserUid = userUid,
            UserName = userName,
            SubTotal = amount,
            DiscountAmount = 0,
            TaxAmount = 0,
            OtherCharges = 0,
            NetAmount = amount,
            PaidAmount = 0,
            BalanceAmount = amount,
            PaymentStatus = "Unpaid",
            InvoiceStatus = "Completed",
            Remarks = remarks,
            CreatedDate = DateTime.Now
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.SalesInvoices.Add(invoice);
            customer.OpeningBalance = amount;
            customer.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);

            var applied = await _advance.AutoApplyAdvanceFifoAsync(customer.Uid, userUid, terminalUid: null, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(),
                $"Opening/previous balance invoice {invoice.InvoiceNo} for {customer.CustomerCode}: {amount:N2}" +
                (applied > 0 ? $"; advance applied {applied:N2}" : ""), ct);
            await tx.CommitAsync(ct);

            TempData["Success"] = applied > 0
                ? $"Previous balance {amount:N2} posted as {invoice.InvoiceNo}. Advance cleared {applied:N2} against dues."
                : $"Previous balance {amount:N2} posted as unpaid invoice {invoice.InvoiceNo}.";
            TempData["OpenTab"] = "arrears";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Could not post previous balance: {ex.Message}";
            TempData["OpenTab"] = "arrears";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Posts customer advance credit. Excess also clears oldest unpaid dues (FIFO).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateAdvancePayment(
        int id,
        decimal amount,
        DateTime? paymentDate,
        string? note,
        CancellationToken ct)
    {
        EnsureCustomersNav();
        await _advance.EnsureSchemaAsync(ct);

        var customer = await _db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (customer is null)
            return NotFound();

        if (amount <= 0)
        {
            TempData["Error"] = "Advance amount must be greater than zero.";
            TempData["OpenTab"] = "advance";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (CustomerClassification.IsWalkIn(customer.CustomerCode, customer.CustomerName))
        {
            TempData["Error"] = "Cannot post advance for the walk-in customer.";
            TempData["OpenTab"] = "advance";
            return RedirectToAction(nameof(Details), new { id });
        }

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
            userUid = parsedUserId;

        var payDate = (paymentDate ?? DateTime.Today).Date;
        var notePart = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var remarks = notePart is null
            ? "Advance payment"
            : $"Advance payment: {notePart}";
        if (remarks.Length > 500)
            remarks = remarks[..500];

        var paymentNo = $"ADV-{DateTime.Now:yyyyMMddHHmmss}";
        while (await _db.CustomerPayments.AnyAsync(x => x.PaymentNo == paymentNo, ct))
            paymentNo = $"ADV-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(10, 99)}";

        var payment = new CustomerPayment
        {
            PaymentNo = paymentNo,
            PaymentDate = payDate,
            CustomerUid = id,
            SalesInvoiceUid = null,
            Amount = amount,
            AppliedToInvoiceAmount = 0,
            UserUid = userUid,
            Remarks = remarks,
            CreatedDate = DateTime.Now
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.CustomerPayments.Add(payment);
            await _advance.ApplyNewPaymentAsync(payment, ct);
            var appliedToDues = await _advance.AutoApplyAdvanceFifoAsync(id, userUid, terminalUid: null, ct);
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Payment", "CustomerPayments", payment.Uid.ToString(),
                $"Advance {payment.PaymentNo} for {customer.CustomerCode}: {amount:N2}" +
                (appliedToDues > 0 ? $"; applied to dues {appliedToDues:N2}" : ""), ct);
            await tx.CommitAsync(ct);

            var balance = await _advance.GetAdvanceBalanceAsync(id, ct);
            TempData["Success"] = appliedToDues > 0
                ? $"Advance {amount:N2} posted. Applied {appliedToDues:N2} to unpaid dues; remaining advance credit {balance:N2}."
                : $"Advance {amount:N2} posted. Advance credit now {balance:N2}.";
            TempData["OpenTab"] = "advance";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            TempData["Error"] = $"Could not post advance: {ex.Message}";
            TempData["OpenTab"] = "advance";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateCustomerDetailsExtrasAsync(int customerUid, CancellationToken ct)
    {
        var unpaid = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid
                        && x.InvoiceStatus != "Cancelled"
                        && x.BalanceAmount > 0)
            .OrderBy(x => x.InvoiceDate)
            .ThenBy(x => x.Uid)
            .Select(x => new
            {
                x.Uid,
                x.InvoiceNo,
                x.InvoiceDate,
                x.BalanceAmount,
                x.Remarks
            })
            .ToListAsync(ct);

        ViewBag.OutstandingDues = unpaid.Sum(x => x.BalanceAmount);
        ViewBag.UnpaidInvoices = unpaid
            .Select(x => new CustomerUnpaidInvoiceRowViewModel
            {
                Uid = x.Uid,
                InvoiceNo = x.InvoiceNo,
                InvoiceDate = x.InvoiceDate,
                BalanceAmount = x.BalanceAmount,
                Label = CustomerClassification.GetSalesTypeLabel("", "", x.Remarks)
            })
            .ToList();

        var today = DateTime.Today;
        var currentMonthStart = new DateTime(today.Year, today.Month, 1);
        var nextMonthStart = currentMonthStart.AddMonths(1);
        var previousMonthStart = currentMonthStart.AddMonths(-1);

        // Monthly Supply uses one invoice per month (InvoiceDate = month start) with daily EntryDate lines.
        // Expand those into day rows so Current/Previous Month tabs show every bill day.
        ViewBag.CurrentMonthBills = await BuildCustomerBillRowsAsync(
            customerUid, currentMonthStart, today, ct);
        ViewBag.PreviousMonthBills = await BuildCustomerBillRowsAsync(
            customerUid, previousMonthStart, currentMonthStart.AddDays(-1), ct);

        ViewBag.CurrentMonthLabel = currentMonthStart.ToString("MMMM yyyy");
        ViewBag.PreviousMonthLabel = previousMonthStart.ToString("MMMM yyyy");
    }

    private async Task<List<CustomerBillHistoryRowViewModel>> BuildCustomerBillRowsAsync(
        int customerUid,
        DateTime rangeStart,
        DateTime rangeEndInclusive,
        CancellationToken ct)
    {
        var rangeEndExclusive = rangeEndInclusive.Date.AddDays(1);
        var monthStart = new DateTime(rangeStart.Year, rangeStart.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        // Pull invoices that belong to this calendar month window (header date or monthly-supply month).
        var invoices = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid
                        && x.InvoiceStatus != SalesReturnsController.ReturnStatus
                        && x.InvoiceStatus != WastageController.WastageStatus
                        && x.InvoiceDate >= monthStart
                        && x.InvoiceDate < monthEnd)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Uid)
            .Select(x => new
            {
                x.Uid,
                x.InvoiceNo,
                x.InvoiceDate,
                x.SubTotal,
                x.OtherCharges,
                x.NetAmount,
                x.PaidAmount,
                x.BalanceAmount,
                x.PaymentStatus,
                x.InvoiceStatus,
                x.Remarks
            })
            .ToListAsync(ct);

        if (invoices.Count == 0)
            return [];

        var invoiceIds = invoices.Select(x => x.Uid).ToList();
        var details = await _db.SalesInvoiceDetails.AsNoTracking()
            .Where(d => invoiceIds.Contains(d.SalesInvoiceUid))
            .Select(d => new
            {
                d.SalesInvoiceUid,
                d.ProductUid,
                d.ProductName,
                d.ProductCode,
                d.Quantity,
                d.UnitPrice,
                d.LineTotal,
                d.DeliveryCharges,
                d.Remarks
            })
            .ToListAsync(ct);

        var detailsByInvoice = details.GroupBy(d => d.SalesInvoiceUid)
            .ToDictionary(g => g.Key, g => g.ToList());

        var rows = new List<CustomerBillHistoryRowViewModel>();

        foreach (var inv in invoices)
        {
            var label = CustomerClassification.GetSalesTypeLabel("", "", inv.Remarks);
            var isMonthly = CustomerClassification.IsMonthlySupplyInvoice(inv.Remarks);
            detailsByInvoice.TryGetValue(inv.Uid, out var lines);
            lines ??= [];

            if (isMonthly && lines.Count > 0)
            {
                // One row per product line per supply day (same columns as Monthly Supply Items).
                var dayLines = lines
                    .Select(l =>
                    {
                        DateTime? entryDate = null;
                        if (!string.IsNullOrWhiteSpace(l.Remarks)
                            && l.Remarks.StartsWith(MonthlyEntryDatePrefix, StringComparison.OrdinalIgnoreCase)
                            && DateTime.TryParse(l.Remarks[MonthlyEntryDatePrefix.Length..], CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out var parsed))
                        {
                            entryDate = parsed.Date;
                        }

                        return new { Line = l, EntryDate = entryDate ?? inv.InvoiceDate.Date };
                    })
                    .Where(x => x.EntryDate >= rangeStart.Date && x.EntryDate < rangeEndExclusive)
                    .OrderByDescending(x => x.EntryDate)
                    .ThenBy(x => x.Line.ProductName);

                foreach (var dayLine in dayLines)
                {
                    var l = dayLine.Line;
                    var itemsAmount = l.LineTotal;
                    var deliveryCharges = Math.Max(0, l.DeliveryCharges);
                    var itemTotal = itemsAmount + deliveryCharges;
                    var name = string.IsNullOrWhiteSpace(l.ProductName) ? l.ProductCode : l.ProductName;

                    rows.Add(new CustomerBillHistoryRowViewModel
                    {
                        Uid = inv.Uid,
                        InvoiceNo = inv.InvoiceNo,
                        InvoiceDate = dayLine.EntryDate,
                        UnitPrice = l.UnitPrice,
                        Quantity = l.Quantity,
                        ItemsTotal = itemsAmount,
                        DeliveryCharges = deliveryCharges,
                        NetAmount = itemTotal,
                        PaidAmount = 0,
                        BalanceAmount = itemTotal,
                        PaymentStatus = inv.PaymentStatus,
                        InvoiceStatus = inv.InvoiceStatus,
                        Label = label,
                        ItemsSummary = name ?? "—",
                        IsMonthlyDayBill = true
                    });
                }
            }
            else if (inv.InvoiceDate.Date >= rangeStart.Date && inv.InvoiceDate.Date < rangeEndExclusive)
            {
                if (lines.Count == 0)
                {
                    var deliveryCharges = Math.Max(0, inv.OtherCharges);
                    var itemsAmount = inv.SubTotal;
                    var itemTotal = itemsAmount + deliveryCharges;
                    rows.Add(new CustomerBillHistoryRowViewModel
                    {
                        Uid = inv.Uid,
                        InvoiceNo = inv.InvoiceNo,
                        InvoiceDate = inv.InvoiceDate.Date,
                        UnitPrice = 0,
                        Quantity = 0,
                        ItemsTotal = itemsAmount,
                        DeliveryCharges = deliveryCharges,
                        NetAmount = itemTotal > 0 ? itemTotal : inv.NetAmount,
                        PaidAmount = inv.PaidAmount,
                        BalanceAmount = inv.BalanceAmount,
                        PaymentStatus = inv.PaymentStatus,
                        InvoiceStatus = inv.InvoiceStatus,
                        Label = label,
                        ItemsSummary = "—",
                        IsMonthlyDayBill = false
                    });
                }
                else
                {
                    foreach (var l in lines)
                    {
                        var itemsAmount = l.LineTotal;
                        var deliveryCharges = lines.Count == 1 ? Math.Max(0, inv.OtherCharges) : 0m;
                        var itemTotal = itemsAmount + deliveryCharges;
                        var name = string.IsNullOrWhiteSpace(l.ProductName) ? l.ProductCode : l.ProductName;

                        rows.Add(new CustomerBillHistoryRowViewModel
                        {
                            Uid = inv.Uid,
                            InvoiceNo = inv.InvoiceNo,
                            InvoiceDate = inv.InvoiceDate.Date,
                            UnitPrice = l.UnitPrice,
                            Quantity = l.Quantity,
                            ItemsTotal = itemsAmount,
                            DeliveryCharges = deliveryCharges,
                            NetAmount = itemTotal,
                            PaidAmount = inv.PaidAmount,
                            BalanceAmount = inv.BalanceAmount,
                            PaymentStatus = inv.PaymentStatus,
                            InvoiceStatus = inv.InvoiceStatus,
                            Label = label,
                            ItemsSummary = name ?? "—",
                            IsMonthlyDayBill = false
                        });
                    }
                }
            }
        }

        return rows
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Uid)
            .ToList();
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
        return RedirectToAction(nameof(Details), new { id });
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

    /// <summary>Appends an area to Configuration key "Areas" (comma-separated) for customer dropdowns.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddArea([FromForm] string? areaName, CancellationToken ct)
    {
        EnsureCustomersNav();
        var name = (areaName ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return BadRequest(new { success = false, message = "Area name is required." });
        }

        if (name.Contains(','))
        {
            return BadRequest(new { success = false, message = "Area name cannot contain a comma." });
        }

        if (name.Length > 100)
        {
            return BadRequest(new { success = false, message = "Area name is too long." });
        }

        var config = await _db.Configurations
            .FirstOrDefaultAsync(x => x.ConfigKey == "Areas", ct);

        var values = (config?.ConfigValue ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        if (values.Any(x => string.Equals(x, name, StringComparison.OrdinalIgnoreCase)))
        {
            return Ok(new { success = true, area = name, message = "Area already exists.", created = false });
        }

        values.Add(name);
        values = values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var csv = string.Join(", ", values);

        if (config is null)
        {
            config = new Configuration
            {
                ConfigKey = "Areas",
                ConfigValue = csv
            };
            _db.Configurations.Add(config);
            await _db.SaveChangesAsync(ct);
            if (config.ConfigId is null)
            {
                config.ConfigId = config.Uid;
                await _db.SaveChangesAsync(ct);
            }

            await _audit.WriteAsync("Create", "Configuration", config.Uid.ToString(),
                $"Created Areas config with area '{name}'", ct);
        }
        else
        {
            config.ConfigValue = csv;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Update", "Configuration", config.Uid.ToString(),
                $"Added area '{name}' to Areas config", ct);
        }

        return Ok(new { success = true, area = name, message = "Area added.", created = true });
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
