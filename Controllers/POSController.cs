using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.Options;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize]
public class POSController : Controller
{
    private readonly INavContextService _navContext;
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly ISyncOutboxService _syncOutbox;
    private readonly IInventoryLedgerService _inventory;
    private readonly ICustomerAdvanceService _advance;
    private readonly string _shopName;

    public POSController(
        INavContextService navContext,
        POSDbContext db,
        IAuditService audit,
        ISyncOutboxService syncOutbox,
        IInventoryLedgerService inventory,
        ICustomerAdvanceService advance,
        IOptions<PosHubOptions> hubOptions)
    {
        _navContext = navContext;
        _db = db;
        _audit = audit;
        _syncOutbox = syncOutbox;
        _inventory = inventory;
        _advance = advance;
        _shopName = string.IsNullOrWhiteSpace(hubOptions.Value.ShopName)
            ? "Shop"
            : hubOptions.Value.ShopName.Trim();
    }

    [HttpGet]
    public IActionResult Index()
    {
        _navContext.SetArea("POS");
        ViewData["Title"] = "POS";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Cash(CancellationToken ct)
    {
        return await SalePage("Cash", ct);
    }

    private const string MonthlyRemark = "Monthly POS";
    private const string MonthlySupplyRemarkPrefix = "Monthly Supply";
    private const string MonthlyEntryDatePrefix = "EntryDate:";

    [HttpGet]
    public async Task<IActionResult> MonthlySupply(int? openCustomer, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        ViewData["Title"] = "Monthly Supply";

        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.IsActive
                        && x.CustomerCode != "C001"
                        && !x.CustomerName.Contains("Walk-in")
                        && !x.CustomerName.Contains("Walk-In")
                        && !x.CustomerName.Contains("Walk In"))
            .OrderBy(x => x.CustomerName)
            .Select(x => new MonthlySupplyCustomerRowViewModel
            {
                Uid = x.Uid,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName,
                MobileNo = x.MobileNo,
                PhoneNo = x.PhoneNo,
                Area = x.Area
            })
            .ToListAsync(ct);

        return View(new MonthlySupplyPageViewModel
        {
            Customers = customers,
            OpenCustomerUid = openCustomer
        });
    }

    /// <summary>
    /// Paper handover for the delivery run: names, locations, and items for the supply date.
    /// Selected customers are used when any are checked; otherwise every customer with items that day.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrintMonthlyDeliveryList(DateTime supplyDate, int[]? selectedCustomerUids, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        var day = supplyDate == default ? DateTime.Today : supplyDate.Date;
        var selected = selectedCustomerUids?.Where(x => x > 0).Distinct().ToList() ?? [];

        var query = _db.Customers.AsNoTracking()
            .Where(x => x.IsActive
                        && x.CustomerCode != "C001"
                        && !x.CustomerName.Contains("Walk-in")
                        && !x.CustomerName.Contains("Walk-In")
                        && !x.CustomerName.Contains("Walk In"));
        if (selected.Count > 0)
        {
            query = query.Where(x => selected.Contains(x.Uid));
        }

        var customers = await query
            .OrderBy(x => x.Area == null || x.Area == "")
            .ThenBy(x => x.Area)
            .ThenBy(x => x.CustomerName)
            .ToListAsync(ct);

        var ids = customers.Select(x => x.Uid).ToList();
        var templates = ids.Count == 0
            ? []
            : await _db.CustomerMonthlySupplies.AsNoTracking()
                .Where(x => x.CustomerUid != null && ids.Contains(x.CustomerUid.Value))
                .ToListAsync(ct);
        var overrides = ids.Count == 0
            ? []
            : await _db.CustomerMonthlySupplyOverrides.AsNoTracking()
                .Where(x => ids.Contains(x.CustomerUid) && x.SupplyDate == day)
                .ToListAsync(ct);

        var rows = new List<DeliveryHandoverRowViewModel>();
        foreach (var customer in customers)
        {
            var lines = BuildEffectiveMonthlySupplyLines(
                templates.Where(x => x.CustomerUid == customer.Uid).OrderBy(x => x.ProductName).ToList(),
                overrides.Where(x => x.CustomerUid == customer.Uid).OrderBy(x => x.ProductName).ToList());
            var items = lines
                .Where(x => !x.IsSkipped && x.Quantity > 0)
                .Select(x => new DeliveryHandoverItemViewModel
                {
                    ProductCode = x.ProductCode,
                    ProductName = x.ProductName,
                    Quantity = x.Quantity
                })
                .ToList();
            if (items.Count == 0)
            {
                continue;
            }

            var phone = !string.IsNullOrWhiteSpace(customer.MobileNo)
                ? customer.MobileNo.Trim()
                : customer.PhoneNo?.Trim();
            rows.Add(new DeliveryHandoverRowViewModel
            {
                CustomerCode = customer.CustomerCode,
                CustomerName = customer.CustomerName,
                Location = FormatDeliveryLocation(customer),
                Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                Items = items
            });
        }

        return View(new DeliveryHandoverPageViewModel
        {
            ShopName = _shopName,
            SupplyDate = day,
            PrintedAt = DateTime.Now,
            SelectedOnly = selected.Count > 0,
            Customers = rows
        });
    }

    private static string FormatDeliveryLocation(Customer customer)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(customer.Area))
        {
            parts.Add(customer.Area.Trim());
        }

        if (!string.IsNullOrWhiteSpace(customer.City)
            && !parts.Contains(customer.City.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            parts.Add(customer.City.Trim());
        }

        if (!string.IsNullOrWhiteSpace(customer.Address))
        {
            parts.Add(customer.Address.Trim());
        }

        return parts.Count == 0 ? "—" : string.Join(", ", parts);
    }

    [HttpGet]
    public async Task<IActionResult> CustomerMonthlySupplyItems(int customerUid, CancellationToken ct)
    {
        if (customerUid <= 0)
        {
            return BadRequest(new { error = "Invalid customer." });
        }

        var exists = await _db.Customers.AsNoTracking()
            .AnyAsync(x => x.Uid == customerUid && x.IsActive, ct);
        if (!exists)
        {
            return NotFound(new { error = "Customer was not found." });
        }

        var items = await _db.CustomerMonthlySupplies.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid)
            .OrderByDescending(x => x.Uid)
            .Select(x => new MonthlySupplyItemRowViewModel
            {
                Uid = x.Uid,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                BrandName = x.BrandName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                DeliveryCharges = x.DeliveryCharges ?? 0m
            })
            .ToListAsync(ct);

        return Json(items);
    }

    /// <summary>
    /// Standing template + day overrides + effective lines for a supply date.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CustomerMonthlySupplyForDate(int customerUid, DateTime supplyDate, CancellationToken ct)
    {
        if (customerUid <= 0)
        {
            return BadRequest(new { error = "Invalid customer." });
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == customerUid && x.IsActive, ct);
        if (customer is null)
        {
            return NotFound(new { error = "Customer was not found." });
        }

        var day = supplyDate.Date;
        var templateRows = await _db.CustomerMonthlySupplies.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid)
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);
        var overrideRows = await _db.CustomerMonthlySupplyOverrides.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid && x.SupplyDate == day)
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);
        var effective = BuildEffectiveMonthlySupplyLines(templateRows, overrideRows);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductName)
            .Select(x => new
            {
                uid = x.Uid,
                code = x.ProductCode,
                name = x.ProductName,
                brand = x.BrandName,
                salePrice = x.SalePrice
            })
            .Take(500)
            .ToListAsync(ct);

        return Json(new
        {
            supplyDate = day.ToString("yyyy-MM-dd"),
            template = templateRows.Select(x => new
            {
                x.Uid,
                productUid = x.ProductUid ?? 0,
                x.ProductCode,
                x.ProductName,
                x.BrandName,
                x.Quantity,
                x.UnitPrice,
                deliveryCharges = x.DeliveryCharges ?? 0m
            }),
            overrides = overrideRows.Select(x => new
            {
                x.Uid,
                productUid = x.ProductUid,
                x.ProductCode,
                x.ProductName,
                x.BrandName,
                x.Quantity,
                x.UnitPrice,
                x.Remarks
            }),
            effective = effective.Select(x => new
            {
                x.ProductUid,
                x.ProductCode,
                x.ProductName,
                x.BrandName,
                x.Quantity,
                x.UnitPrice,
                x.IsOverride,
                x.IsSkipped
            }),
            products
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpsertMonthlySupplyOverride(
        [FromForm] int customerUid,
        [FromForm] int productUid,
        [FromForm] DateTime supplyDate,
        [FromForm] decimal quantity,
        [FromForm] decimal unitPrice,
        [FromForm] string? remarks,
        CancellationToken ct)
    {
        _navContext.SetArea("POS");
        if (customerUid <= 0 || productUid <= 0)
        {
            return BadRequest(new { success = false, message = "Customer and product are required." });
        }

        var day = supplyDate.Date;
        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == customerUid && x.IsActive, ct);
        if (customer is null)
        {
            return NotFound(new { success = false, message = "Customer was not found." });
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == productUid && x.IsActive, ct);
        if (product is null)
        {
            return NotFound(new { success = false, message = "Product was not found." });
        }

        if (quantity < 0)
        {
            quantity = 0;
        }

        var templatePrice = await _db.CustomerMonthlySupplies.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid && x.ProductUid == productUid)
            .Select(x => (decimal?)x.UnitPrice)
            .FirstOrDefaultAsync(ct);

        var price = unitPrice > 0
            ? unitPrice
            : (templatePrice is > 0 ? templatePrice.Value : product.SalePrice);

        var row = await _db.CustomerMonthlySupplyOverrides
            .FirstOrDefaultAsync(x => x.CustomerUid == customerUid
                                      && x.ProductUid == productUid
                                      && x.SupplyDate == day, ct);

        if (row is null)
        {
            row = new CustomerMonthlySupplyOverride
            {
                CustomerUid = customer.Uid,
                CustomerCode = customer.CustomerCode,
                ProductUid = product.Uid,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                BrandName = product.BrandName,
                SupplyDate = day,
                Quantity = quantity,
                UnitPrice = price,
                Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim(),
                CreatedDate = DateTime.Now
            };
            _db.CustomerMonthlySupplyOverrides.Add(row);
        }
        else
        {
            row.Quantity = quantity;
            row.UnitPrice = price;
            row.ProductCode = product.ProductCode;
            row.ProductName = product.ProductName;
            row.BrandName = product.BrandName;
            row.Remarks = string.IsNullOrWhiteSpace(remarks) ? null : remarks.Trim();
            row.UpdatedDate = DateTime.Now;
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Upsert", "CustomerMonthlySupplyOverrides", row.Uid.ToString(),
            $"Override {product.ProductCode} qty={quantity} for {customer.CustomerCode} on {day:yyyy-MM-dd}", ct);

        return Json(new
        {
            success = true,
            message = quantity <= 0
                ? $"Skip set for {product.ProductName} on {day:yyyy-MM-dd}."
                : $"Override saved for {product.ProductName} on {day:yyyy-MM-dd}."
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMonthlySupplyOverride(
        [FromForm] int id,
        [FromForm] int? customerUid,
        CancellationToken ct)
    {
        _navContext.SetArea("POS");
        var row = await _db.CustomerMonthlySupplyOverrides.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (row is null)
        {
            return NotFound(new { success = false, message = "Override was not found." });
        }

        if (customerUid.HasValue && row.CustomerUid != customerUid.Value)
        {
            return BadRequest(new { success = false, message = "Override does not belong to this customer." });
        }

        _db.CustomerMonthlySupplyOverrides.Remove(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Delete", "CustomerMonthlySupplyOverrides", id.ToString(),
            $"Removed override {row.ProductCode} for {row.CustomerCode} on {row.SupplyDate:yyyy-MM-dd}", ct);

        return Json(new { success = true, message = "Override removed — standing supply applies again." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MonthlySupplyContinue(int[]? selectedCustomerUids)
    {
        _navContext.SetArea("POS");
        if (selectedCustomerUids is null || selectedCustomerUids.Length == 0)
        {
            TempData["Error"] = "Select at least one customer.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        var ids = string.Join(",", selectedCustomerUids.Distinct());
        return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = ids });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMonthlySupplied(DateTime supplyDate, int[]? selectedCustomerUids, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        var customerIds = selectedCustomerUids?.Distinct().Where(x => x > 0).ToList() ?? [];
        if (customerIds.Count == 0)
        {
            TempData["Error"] = "Select at least one customer.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        var supplyDay = supplyDate.Date;
        if (supplyDay == default)
        {
            TempData["Error"] = "Select a supply date.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        var customers = await _db.Customers.AsNoTracking()
            .Where(x => customerIds.Contains(x.Uid) && x.IsActive)
            .OrderBy(x => x.CustomerName)
            .ToListAsync(ct);

        if (customers.Count == 0)
        {
            TempData["Error"] = "Selected customers were not found.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        int? userUid = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var parsedUserId))
        {
            userUid = parsedUserId;
        }

        var branchUid = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var paymentMethodUid = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.PaymentName)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var processed = new List<string>();
        var skipped = new List<string>();
        var monthStart = new DateTime(supplyDay.Year, supplyDay.Month, 1);
        var monthEnd = monthStart.AddMonths(1);
        var entryRemark = $"{MonthlyEntryDatePrefix}{supplyDay:yyyy-MM-dd}";

        try
        {
            await using var tx = await _db.Database.BeginTransactionAsync(ct);

            foreach (var customer in customers)
            {
                if (IsWalkInCustomer(customer.CustomerCode, customer.CustomerName))
                {
                    skipped.Add($"{customer.CustomerName} (walk-in)");
                    continue;
                }

                var monthlyItems = await _db.CustomerMonthlySupplies.AsNoTracking()
                    .Where(x => x.CustomerUid == customer.Uid)
                    .OrderBy(x => x.ProductName)
                    .ToListAsync(ct);

                var dayOverrides = await _db.CustomerMonthlySupplyOverrides.AsNoTracking()
                    .Where(x => x.CustomerUid == customer.Uid && x.SupplyDate == supplyDay)
                    .ToListAsync(ct);

                var effectiveLines = BuildEffectiveMonthlySupplyLines(monthlyItems, dayOverrides)
                    .Where(x => !x.IsSkipped && x.Quantity > 0 && x.ProductUid > 0)
                    .ToList();
                if (effectiveLines.Count == 0)
                {
                    skipped.Add($"{customer.CustomerName} (no supply lines for {supplyDay:yyyy-MM-dd} — add standing items or clear skip overrides)");
                    continue;
                }

                var productUids = effectiveLines
                    .Select(x => x.ProductUid)
                    .Distinct()
                    .ToList();

                var products = await _db.Products.AsNoTracking()
                    .Where(x => productUids.Contains(x.Uid))
                    .ToDictionaryAsync(x => x.Uid, ct);

                // One Monthly Supply invoice per customer per calendar month.
                var invoice = await _db.SalesInvoices
                    .Where(x => x.CustomerUid == customer.Uid
                                && x.InvoiceStatus != "Cancelled"
                                && x.Remarks != null
                                && x.Remarks.StartsWith(MonthlySupplyRemarkPrefix)
                                && x.InvoiceDate >= monthStart
                                && x.InvoiceDate < monthEnd)
                    .OrderBy(x => x.Uid)
                    .FirstOrDefaultAsync(ct);

                var createdNew = false;
                if (invoice is null)
                {
                    var invoiceNo = $"POS-MS-{supplyDay:yyyyMM}-{customer.CustomerCode}";
                    while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
                    {
                        invoiceNo = $"POS-MS-{supplyDay:yyyyMM}-{customer.CustomerCode}-{Random.Shared.Next(10, 99)}";
                    }

                    invoice = new SalesInvoice
                    {
                        ClientTransactionId = Guid.NewGuid(),
                        InvoiceNo = invoiceNo,
                        InvoiceDate = monthStart,
                        BranchUid = branchUid,
                        CustomerUid = customer.Uid,
                        UserUid = userUid,
                        PaymentMethodUid = paymentMethodUid,
                        SubTotal = 0,
                        DiscountAmount = 0,
                        TaxAmount = 0,
                        OtherCharges = 0,
                        NetAmount = 0,
                        PaidAmount = 0,
                        BalanceAmount = 0,
                        PaymentStatus = "Unpaid",
                        InvoiceStatus = "Completed",
                        Remarks = $"{MonthlySupplyRemarkPrefix} - {customer.CustomerCode} - {customer.CustomerName} - {supplyDay:yyyy-MM}",
                        CreatedDate = DateTime.Now
                    };

                    _db.SalesInvoices.Add(invoice);
                    await _db.SaveChangesAsync(ct);
                    createdNew = true;
                }
                else if (invoice.ClientTransactionId is null)
                {
                    invoice.ClientTransactionId = Guid.NewGuid();
                }

                var existingDayLines = await _db.SalesInvoiceDetails
                    .Where(x => x.SalesInvoiceUid == invoice.Uid
                                && x.Remarks != null
                                && x.Remarks == entryRemark)
                    .ToListAsync(ct);
                var existingByProduct = existingDayLines
                    .GroupBy(x => x.ProductUid)
                    .ToDictionary(g => g.Key, g => g.OrderBy(x => x.Uid).First());

                var added = 0;
                var updated = 0;
                var stockResult = new InventoryApplyResult();
                foreach (var line in effectiveLines)
                {
                    products.TryGetValue(line.ProductUid, out var product);
                    var quantity = line.Quantity < 0.1m ? 0.1m : line.Quantity;
                    var unitPrice = line.UnitPrice;
                    var lineTotal = Math.Round(unitPrice * quantity, 2);
                    var previousQty = 0m;

                    if (existingByProduct.TryGetValue(line.ProductUid, out var detail))
                    {
                        previousQty = detail.Quantity;
                        detail.ProductCode = line.ProductCode;
                        detail.ProductName = line.ProductName;
                        detail.Quantity = quantity;
                        detail.UnitPrice = unitPrice;
                        detail.LineTotal = lineTotal;
                        detail.TaxPercent = product?.TaxPercent ?? detail.TaxPercent;
                        detail.CostPrice = product?.PurchasePrice ?? detail.CostPrice;
                        detail.Remarks = entryRemark;
                        updated++;
                    }
                    else
                    {
                        _db.SalesInvoiceDetails.Add(new SalesInvoiceDetail
                        {
                            SalesInvoiceUid = invoice.Uid,
                            ProductUid = line.ProductUid,
                            ProductCode = line.ProductCode,
                            ProductName = line.ProductName,
                            Quantity = quantity,
                            UnitPrice = unitPrice,
                            DiscountPercent = 0,
                            DiscountAmount = 0,
                            TaxPercent = product?.TaxPercent ?? 0,
                            TaxAmount = 0,
                            LineTotal = lineTotal,
                            CostPrice = product?.PurchasePrice ?? 0,
                            Remarks = entryRemark
                        });
                        added++;
                    }

                    var delta = quantity - previousQty;
                    if (delta != 0)
                    {
                        var deltaResult = await _inventory.ApplySaleQuantityDeltaAsync(
                            invoice.Uid,
                            invoice.InvoiceNo,
                            new InventoryLineMovement
                            {
                                ProductUid = line.ProductUid,
                                ProductName = line.ProductName,
                                Quantity = delta,
                                UnitCost = unitPrice
                            },
                            warehouseUid: null,
                            branchUid,
                            userUid,
                            terminalUid: null,
                            ct);
                        stockResult.ShortageNotes.AddRange(deltaResult.ShortageNotes);
                    }
                }

                await _db.SaveChangesAsync(ct);
                await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
                await ApplyMonthlySupplyDeliveryChargesAsync(invoice, customer.Uid, ct);
                await SyncInvoicePaidFromCustomerPaymentsAsync(invoice, ct);
                var advanceApplied = await _advance.AutoApplyAdvanceToInvoiceAsync(invoice, userUid, terminalUid: null, ct);
                await SyncInvoicePaidFromCustomerPaymentsAsync(invoice, ct);
                if (stockResult.HasShortages)
                    invoice.Remarks = _inventory.MergeShortageRemarks(invoice.Remarks, stockResult);
                invoice.UpdatedDate = DateTime.Now;
                invoice.UserUid ??= userUid;
                await _db.SaveChangesAsync(ct);

                var advanceNote = advanceApplied > 0 ? $", advance applied {advanceApplied:N2}" : "";
                if (createdNew)
                {
                    await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(),
                        $"Monthly supply invoice {invoice.InvoiceNo} for {customer.CustomerCode} ({supplyDay:yyyy-MM}) with {added} detail line(s); OtherCharges={invoice.OtherCharges:N2}{advanceNote}.", ct);
                    processed.Add($"{customer.CustomerCode}: created {invoice.InvoiceNo} (+{added} lines for {supplyDay:yyyy-MM-dd}, delivery {invoice.OtherCharges:N2}{advanceNote})");
                }
                else
                {
                    await _audit.WriteAsync("Update", "SalesInvoices", invoice.Uid.ToString(),
                        $"Monthly supply {invoice.InvoiceNo} for {customer.CustomerCode} on {supplyDay:yyyy-MM-dd}: +{added} / ~{updated} line(s); OtherCharges={invoice.OtherCharges:N2}{advanceNote}.", ct);
                    processed.Add($"{customer.CustomerCode}: {invoice.InvoiceNo} (+{added}/~{updated} lines for {supplyDay:yyyy-MM-dd}, delivery {invoice.OtherCharges:N2}{advanceNote})");
                }

                if (invoice.ClientTransactionId is Guid supplyTx)
                {
                    await _syncOutbox.EnqueueSalesInvoiceAsync(invoice.Uid, supplyTx, ct);
                }
            }

            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Add Supplied failed: " + (ex.InnerException?.Message ?? ex.Message);
            return RedirectToAction(nameof(MonthlySupply));
        }

        if (processed.Count == 0)
        {
            TempData["Error"] = skipped.Count > 0
                ? "No invoices processed. " + string.Join("; ", skipped)
                : "No invoices were processed.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        var message = $"Monthly supply posted: {string.Join("; ", processed)}.";
        if (skipped.Count > 0)
        {
            message += " Skipped: " + string.Join("; ", skipped) + ".";
        }

        TempData["Success"] = message;
        return RedirectToAction(nameof(MonthlySupply));
    }

    [HttpGet]
    public async Task<IActionResult> MonthlySupplyItems(string? customerUids, int? activeCustomerUid, string? returnTo, CancellationToken ct)
    {
        var fromCustomers = IsCustomersReturn(returnTo);
        if (fromCustomers)
            _navContext.SetArea("Customers");
        else
            _navContext.SetArea("POS");
        ViewData["Title"] = "Monthly Supply Items";

        var uidList = ParseCustomerUids(customerUids);
        if (uidList.Count == 0)
        {
            TempData["Error"] = "Select at least one customer.";
            return fromCustomers
                ? RedirectToAction("Index", "Customers")
                : RedirectToAction(nameof(MonthlySupply));
        }

        var selectedCustomers = await _db.Customers.AsNoTracking()
            .Where(x => uidList.Contains(x.Uid) && x.IsActive)
            .OrderBy(x => x.CustomerName)
            .Select(x => new MonthlySupplyCustomerRowViewModel
            {
                Uid = x.Uid,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName,
                MobileNo = x.MobileNo,
                PhoneNo = x.PhoneNo,
                Area = x.Area
            })
            .ToListAsync(ct);

        if (selectedCustomers.Count == 0)
        {
            TempData["Error"] = "Selected customers were not found.";
            return fromCustomers
                ? RedirectToAction("Index", "Customers")
                : RedirectToAction(nameof(MonthlySupply));
        }

        var active = selectedCustomers.FirstOrDefault(x => x.Uid == activeCustomerUid)
                     ?? selectedCustomers[0];

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductName)
            .Select(x => new
            {
                x.Uid,
                x.ProductCode,
                x.ProductName,
                x.BrandName,
                x.SalePrice
            })
            .ToListAsync(ct);

        var productOptions = products.Select(p => new SelectListItem
        {
            Value = p.Uid.ToString(),
            Text = p.ProductName
        }).ToList();

        var items = await _db.CustomerMonthlySupplies.AsNoTracking()
            .Where(x => x.CustomerUid == active.Uid)
            .OrderByDescending(x => x.Uid)
            .Select(x => new MonthlySupplyItemRowViewModel
            {
                Uid = x.Uid,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                BrandName = x.BrandName,
                Quantity = x.Quantity,
                UnitPrice = x.UnitPrice,
                DeliveryCharges = x.DeliveryCharges ?? 0m
            })
            .ToListAsync(ct);

        ViewBag.ProductPrices = products.ToDictionary(x => x.Uid.ToString(), x => x.SalePrice);

        return View(new MonthlySupplyItemsPageViewModel
        {
            SelectedCustomers = selectedCustomers,
            ActiveCustomerUid = active.Uid,
            ActiveCustomerCode = active.CustomerCode,
            ActiveCustomerName = active.CustomerName,
            CustomerUidsQuery = string.Join(",", selectedCustomers.Select(x => x.Uid)),
            Products = productOptions,
            Items = items,
            ReturnTo = fromCustomers ? "Customers" : null
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMonthlySupplyItem(MonthlySupplyAddItemRequest request, CancellationToken ct)
    {
        var fromCustomers = IsCustomersReturn(request.ReturnTo);
        if (fromCustomers)
            _navContext.SetArea("Customers");
        else
            _navContext.SetArea("POS");
        var redirectUids = request.CustomerUids;
        var returnTo = fromCustomers ? "Customers" : null;

        if (!ModelState.IsValid || request.CustomerUid <= 0 || request.ProductUid <= 0)
        {
            TempData["Error"] = "Select a customer and product, then enter quantity and price.";
            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = request.CustomerUid, returnTo });
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.CustomerUid && x.IsActive, ct);
        if (customer is null)
        {
            TempData["Error"] = "Customer was not found.";
            return fromCustomers
                ? RedirectToAction("Index", "Customers")
                : RedirectToAction(nameof(MonthlySupply));
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.ProductUid && x.IsActive, ct);
        if (product is null)
        {
            TempData["Error"] = "Product was not found.";
            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = request.CustomerUid, returnTo });
        }

        var unitPrice = request.UnitPrice > 0 ? request.UnitPrice : product.SalePrice;
        var quantity = request.Quantity < 0.1m ? 0.1m : request.Quantity;

        var deliveryCharges = request.DeliveryCharges < 0 ? 0 : request.DeliveryCharges;

        _db.CustomerMonthlySupplies.Add(new CustomerMonthlySupply
        {
            CustomerUid = customer.Uid,
            CustomerCode = customer.CustomerCode,
            CustomerName = customer.CustomerName,
            ProductUid = product.Uid,
            ProductCode = product.ProductCode,
            ProductName = product.ProductName,
            BrandName = product.BrandName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DeliveryCharges = deliveryCharges,
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "CustomerMonthlySupply", customer.Uid.ToString(),
            $"Added monthly supply item {product.ProductCode} for {customer.CustomerCode}", ct);

        TempData["Success"] = $"Added {product.ProductName} for {customer.CustomerName}.";
        return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = customer.Uid, returnTo });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMonthlySupplyItem(MonthlySupplyUpdateItemRequest request, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        if (!ModelState.IsValid || request.Id <= 0 || request.CustomerUid <= 0)
        {
            TempData["Error"] = "Enter valid quantity, unit price, and delivery charges.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = request.CustomerUid });
        }

        var row = await _db.CustomerMonthlySupplies.FirstOrDefaultAsync(x => x.Uid == request.Id, ct);
        if (row is null || row.CustomerUid != request.CustomerUid)
        {
            TempData["Error"] = "Item was not found for this customer.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = request.CustomerUid });
        }

        var quantity = request.Quantity < 0.1m ? 0.1m : request.Quantity;
        row.Quantity = quantity;
        row.UnitPrice = request.UnitPrice < 0 ? 0 : request.UnitPrice;
        row.DeliveryCharges = request.DeliveryCharges < 0 ? 0 : request.DeliveryCharges;
        row.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "CustomerMonthlySupply", row.Uid.ToString(),
            $"Updated monthly supply item {row.ProductCode} for {row.CustomerCode}", ct);

        TempData["Success"] = "Item updated.";
        return RedirectToAction(nameof(MonthlySupply), new { openCustomer = request.CustomerUid });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMonthlySupplyItem(
        int id,
        string? customerUids,
        int? activeCustomerUid,
        bool fromMonthlySupply = false,
        int? customerUid = null,
        string? returnTo = null,
        CancellationToken ct = default)
    {
        var fromCustomers = IsCustomersReturn(returnTo);
        if (fromCustomers)
            _navContext.SetArea("Customers");
        else
            _navContext.SetArea("POS");
        var returnToValue = fromCustomers ? "Customers" : null;
        var row = await _db.CustomerMonthlySupplies.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (row is null)
        {
            TempData["Error"] = "Item was not found.";
            if (fromMonthlySupply)
            {
                return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid });
            }

            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids, activeCustomerUid, returnTo = returnToValue });
        }

        if (fromMonthlySupply && customerUid.HasValue && row.CustomerUid != customerUid)
        {
            TempData["Error"] = "Item does not belong to this customer.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid });
        }

        var ownerCustomerUid = row.CustomerUid;
        if (ownerCustomerUid is int ownerId && row.ProductUid is int productId)
        {
            var overrides = await _db.CustomerMonthlySupplyOverrides
                .Where(x => x.CustomerUid == ownerId && x.ProductUid == productId)
                .ToListAsync(ct);
            if (overrides.Count > 0)
            {
                _db.CustomerMonthlySupplyOverrides.RemoveRange(overrides);
            }
        }

        _db.CustomerMonthlySupplies.Remove(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Delete", "CustomerMonthlySupply", id.ToString(),
            $"Deleted monthly supply item {row.ProductCode} for {row.CustomerCode}", ct);

        TempData["Success"] = "Item deleted.";
        if (fromMonthlySupply)
        {
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid ?? ownerCustomerUid });
        }

        return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids, activeCustomerUid = activeCustomerUid ?? ownerCustomerUid, returnTo = returnToValue });
    }

    private static bool IsCustomersReturn(string? returnTo) =>
        string.Equals(returnTo, "Customers", StringComparison.OrdinalIgnoreCase);

    private static List<int> ParseCustomerUids(string? customerUids)
    {
        if (string.IsNullOrWhiteSpace(customerUids))
        {
            return [];
        }

        return customerUids
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    private sealed class EffectiveMonthlySupplyLine
    {
        public int ProductUid { get; set; }
        public string ProductCode { get; set; } = "";
        public string ProductName { get; set; } = "";
        public string? BrandName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public bool IsOverride { get; set; }
        public bool IsSkipped { get; set; }
    }

    /// <summary>
    /// Template + day overrides. Qty 0 override skips the product. Extra override products become one-off lines.
    /// </summary>
    private static List<EffectiveMonthlySupplyLine> BuildEffectiveMonthlySupplyLines(
        List<CustomerMonthlySupply> template,
        List<CustomerMonthlySupplyOverride> overrides)
    {
        var overrideByProduct = overrides
            .Where(x => x.ProductUid > 0)
            .GroupBy(x => x.ProductUid)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Uid).First());

        var result = new List<EffectiveMonthlySupplyLine>();
        var usedProducts = new HashSet<int>();

        foreach (var line in template)
        {
            var productUid = line.ProductUid ?? 0;
            if (productUid > 0)
            {
                usedProducts.Add(productUid);
            }

            if (productUid > 0 && overrideByProduct.TryGetValue(productUid, out var ov))
            {
                if (ov.Quantity <= 0)
                {
                    result.Add(new EffectiveMonthlySupplyLine
                    {
                        ProductUid = productUid,
                        ProductCode = ov.ProductCode,
                        ProductName = ov.ProductName,
                        BrandName = ov.BrandName,
                        Quantity = 0,
                        UnitPrice = ov.UnitPrice > 0 ? ov.UnitPrice : line.UnitPrice,
                        IsOverride = true,
                        IsSkipped = true
                    });
                    continue;
                }

                result.Add(new EffectiveMonthlySupplyLine
                {
                    ProductUid = productUid,
                    ProductCode = string.IsNullOrWhiteSpace(ov.ProductCode) ? line.ProductCode : ov.ProductCode,
                    ProductName = string.IsNullOrWhiteSpace(ov.ProductName) ? line.ProductName : ov.ProductName,
                    BrandName = ov.BrandName ?? line.BrandName,
                    Quantity = ov.Quantity,
                    UnitPrice = ov.UnitPrice > 0 ? ov.UnitPrice : line.UnitPrice,
                    IsOverride = true
                });
                continue;
            }

            if (line.Quantity <= 0)
            {
                continue;
            }

            result.Add(new EffectiveMonthlySupplyLine
            {
                ProductUid = productUid,
                ProductCode = line.ProductCode,
                ProductName = line.ProductName,
                BrandName = line.BrandName,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                IsOverride = false
            });
        }

        foreach (var ov in overrides.Where(x => x.ProductUid > 0 && !usedProducts.Contains(x.ProductUid)))
        {
            if (ov.Quantity <= 0)
            {
                continue;
            }

            result.Add(new EffectiveMonthlySupplyLine
            {
                ProductUid = ov.ProductUid,
                ProductCode = ov.ProductCode,
                ProductName = ov.ProductName,
                BrandName = ov.BrandName,
                Quantity = ov.Quantity,
                UnitPrice = ov.UnitPrice,
                IsOverride = true
            });
        }

        return result;
    }

    [HttpGet]
    public async Task<IActionResult> Credit(CancellationToken ct)
    {
        _navContext.SetArea("POS");
        ViewData["Title"] = "Monthly Customer Entry";

        var customers = await _db.Customers.AsNoTracking()
            .Where(x => x.IsActive
                        && x.CustomerCode != "C001"
                        && !x.CustomerName.Contains("Walk-in")
                        && !x.CustomerName.Contains("Walk-In")
                        && !x.CustomerName.Contains("Walk In"))
            .OrderBy(x => x.CustomerName)
            .Select(x => new MonthlyCustomerListItemViewModel
            {
                Uid = x.Uid,
                CustomerCode = x.CustomerCode,
                CustomerName = x.CustomerName
            })
            .ToListAsync(ct);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductName)
            .Select(x => new MonthlyProductOptionViewModel
            {
                Uid = x.Uid,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                SalePrice = x.SalePrice
            })
            .ToListAsync(ct);

        return View("MonthlyEntry", new MonthlyEntryPageViewModel
        {
            Customers = customers,
            Products = products
        });
    }

    [HttpGet]
    public async Task<IActionResult> MonthlyEntries(int customerUid, CancellationToken ct)
    {
        if (customerUid <= 0)
        {
            return BadRequest(new { success = false, message = "Select a customer first." });
        }

        var raw = await (
            from inv in _db.SalesInvoices.AsNoTracking()
            join d in _db.SalesInvoiceDetails.AsNoTracking() on inv.Uid equals d.SalesInvoiceUid
            where inv.CustomerUid == customerUid
                  && inv.Remarks != null && inv.Remarks.StartsWith(MonthlyRemark)
                  && inv.InvoiceStatus != "Cancelled"
            orderby d.Uid descending
            select new
            {
                inv.Uid,
                DetailUid = d.Uid,
                inv.InvoiceDate,
                d.Remarks,
                d.ProductUid,
                ProductCode = d.ProductCode ?? string.Empty,
                ProductName = d.ProductName ?? string.Empty,
                d.Quantity,
                d.UnitPrice,
                d.LineTotal
            }).ToListAsync(ct);

        var rows = raw.Select(x => new MonthlyEntryRowViewModel
        {
            InvoiceUid = x.Uid,
            DetailUid = x.DetailUid,
            EntryDate = ParseEntryDate(x.Remarks, x.InvoiceDate),
            ProductUid = x.ProductUid,
            ProductCode = x.ProductCode,
            ProductName = x.ProductName,
            Quantity = x.Quantity,
            UnitPrice = x.UnitPrice,
            LineTotal = x.LineTotal
        }).ToList();

        return Ok(rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveMonthlyEntry([FromBody] MonthlyEntrySaveRequest request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Invalid request." });
        }

        if (request.CustomerUid <= 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Select a monthly customer." });
        }

        if (request.ProductUid <= 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Select an item." });
        }

        request.Quantity = Math.Round(request.Quantity, 1, MidpointRounding.AwayFromZero);
        if (request.Quantity < 0.1m)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Quantity must be at least 0.1." });
        }

        if (request.UnitPrice < 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Price cannot be negative." });
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.CustomerUid && x.IsActive, ct);
        if (customer is null || IsWalkInCustomer(customer.CustomerCode, customer.CustomerName))
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Monthly customer was not found." });
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.ProductUid && x.IsActive, ct);
        if (product is null)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Item was not found." });
        }

        var entryDate = request.EntryDate.Date;
        var lineTotal = Math.Round(request.UnitPrice * request.Quantity, 2);
        int? userUid = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var parsedUserId))
        {
            userUid = parsedUserId;
        }

        var branchUid = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        SalesInvoice invoice;
        SalesInvoiceDetail detail;

        var stockResult = new InventoryApplyResult();

        if (request.DetailUid is > 0)
        {
            detail = await _db.SalesInvoiceDetails.FirstOrDefaultAsync(x => x.Uid == request.DetailUid, ct);
            if (detail is null)
            {
                return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Entry detail not found." });
            }

            invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x =>
                x.Uid == detail.SalesInvoiceUid
                && x.CustomerUid == request.CustomerUid
                && x.Remarks != null && x.Remarks.StartsWith(MonthlyRemark)
                && x.InvoiceStatus != "Cancelled", ct);
            if (invoice is null)
            {
                return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Draft invoice not found." });
            }

            var oldProductUid = detail.ProductUid;
            var oldProductName = detail.ProductName ?? product.ProductName;
            var oldQty = detail.Quantity;
            var oldPrice = detail.UnitPrice;

            detail.ProductUid = product.Uid;
            detail.ProductCode = product.ProductCode;
            detail.ProductName = product.ProductName;
            detail.Quantity = request.Quantity;
            detail.UnitPrice = request.UnitPrice;
            detail.LineTotal = lineTotal;
            detail.CostPrice = product.PurchasePrice;
            detail.TaxPercent = product.TaxPercent;
            detail.Remarks = $"{MonthlyEntryDatePrefix}{entryDate:yyyy-MM-dd}";

            if (oldProductUid != product.Uid)
            {
                if (oldQty > 0)
                {
                    var restore = await _inventory.ApplySaleQuantityDeltaAsync(
                        invoice.Uid, invoice.InvoiceNo,
                        new InventoryLineMovement
                        {
                            ProductUid = oldProductUid,
                            ProductName = oldProductName,
                            Quantity = -oldQty,
                            UnitCost = oldPrice
                        },
                        null, branchUid, userUid, null, ct);
                    stockResult.ShortageNotes.AddRange(restore.ShortageNotes);
                }

                var deduct = await _inventory.ApplySaleQuantityDeltaAsync(
                    invoice.Uid, invoice.InvoiceNo,
                    new InventoryLineMovement
                    {
                        ProductUid = product.Uid,
                        ProductName = product.ProductName,
                        Quantity = request.Quantity,
                        UnitCost = request.UnitPrice
                    },
                    null, branchUid, userUid, null, ct);
                stockResult.ShortageNotes.AddRange(deduct.ShortageNotes);
            }
            else
            {
                var delta = request.Quantity - oldQty;
                if (delta != 0)
                {
                    var deltaResult = await _inventory.ApplySaleQuantityDeltaAsync(
                        invoice.Uid, invoice.InvoiceNo,
                        new InventoryLineMovement
                        {
                            ProductUid = product.Uid,
                            ProductName = product.ProductName,
                            Quantity = delta,
                            UnitCost = request.UnitPrice
                        },
                        null, branchUid, userUid, null, ct);
                    stockResult.ShortageNotes.AddRange(deltaResult.ShortageNotes);
                }
            }

            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            if (stockResult.HasShortages)
                invoice.Remarks = _inventory.MergeShortageRemarks(invoice.Remarks, stockResult);
            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Update", "SalesInvoiceDetails", detail.Uid.ToString(),
                $"Updated monthly line for {customer.CustomerName}: {product.ProductName}", ct);
        }
        else
        {
            invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x =>
                x.CustomerUid == customer.Uid
                && x.Remarks != null && x.Remarks.StartsWith(MonthlyRemark)
                && x.InvoiceStatus != "Cancelled", ct);

            if (invoice is null)
            {
                var invoiceNo = $"POS-M-{DateTime.Now:yyyyMMddHHmmssfff}";
                while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
                {
                    invoiceNo = $"POS-M-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";
                }

                invoice = new SalesInvoice
                {
                    ClientTransactionId = Guid.NewGuid(),
                    InvoiceNo = invoiceNo,
                    InvoiceDate = entryDate,
                    BranchUid = branchUid,
                    CustomerUid = customer.Uid,
                    UserUid = userUid,
                    SubTotal = 0,
                    DiscountAmount = 0,
                    TaxAmount = 0,
                    OtherCharges = 0,
                    NetAmount = 0,
                    PaidAmount = 0,
                    BalanceAmount = 0,
                    PaymentStatus = "Unpaid",
                    InvoiceStatus = "Completed",
                    Remarks = MonthlyRemark,
                    CreatedDate = DateTime.Now
                };
                _db.SalesInvoices.Add(invoice);
                await _db.SaveChangesAsync(ct);
            }
            else if (invoice.ClientTransactionId is null)
            {
                invoice.ClientTransactionId = Guid.NewGuid();
            }

            detail = new SalesInvoiceDetail
            {
                SalesInvoiceUid = invoice.Uid,
                ProductUid = product.Uid,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                DiscountPercent = 0,
                DiscountAmount = 0,
                TaxPercent = product.TaxPercent,
                TaxAmount = 0,
                LineTotal = lineTotal,
                CostPrice = product.PurchasePrice,
                Remarks = $"{MonthlyEntryDatePrefix}{entryDate:yyyy-MM-dd}"
            };
            _db.SalesInvoiceDetails.Add(detail);
            await _db.SaveChangesAsync(ct);

            var deductNew = await _inventory.ApplySaleQuantityDeltaAsync(
                invoice.Uid, invoice.InvoiceNo,
                new InventoryLineMovement
                {
                    ProductUid = product.Uid,
                    ProductName = product.ProductName,
                    Quantity = request.Quantity,
                    UnitCost = request.UnitPrice
                },
                null, branchUid, userUid, null, ct);
            stockResult.ShortageNotes.AddRange(deductNew.ShortageNotes);

            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            if (stockResult.HasShortages)
                invoice.Remarks = _inventory.MergeShortageRemarks(invoice.Remarks, stockResult);
            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "SalesInvoiceDetails", detail.Uid.ToString(),
                $"Monthly line for {customer.CustomerName}: {product.ProductName}", ct);
        }

        await tx.CommitAsync(ct);

        if (invoice.ClientTransactionId is Guid monthlyTx)
        {
            await _syncOutbox.EnqueueSalesInvoiceAsync(invoice.Uid, monthlyTx, ct);
        }

        return Ok(new MonthlyEntryApiResult
        {
            Success = true,
            Message = request.DetailUid is > 0 ? "Entry updated." : "Entry added.",
            Entry = new MonthlyEntryRowViewModel
            {
                InvoiceUid = invoice.Uid,
                DetailUid = detail.Uid,
                EntryDate = entryDate,
                ProductUid = detail.ProductUid,
                ProductCode = detail.ProductCode ?? string.Empty,
                ProductName = detail.ProductName ?? string.Empty,
                Quantity = detail.Quantity,
                UnitPrice = detail.UnitPrice,
                LineTotal = detail.LineTotal
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteMonthlyEntry(int detailUid, CancellationToken ct)
    {
        var detail = await _db.SalesInvoiceDetails.FirstOrDefaultAsync(x => x.Uid == detailUid, ct);
        if (detail is null)
        {
            return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Entry not found." });
        }

        var invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x =>
            x.Uid == detail.SalesInvoiceUid
            && x.Remarks != null && x.Remarks.StartsWith(MonthlyRemark)
            && x.InvoiceStatus != "Cancelled", ct);
        if (invoice is null)
        {
            return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Draft invoice not found." });
        }

        if (detail.Quantity > 0)
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
                null,
                invoice.BranchUid,
                invoice.UserUid,
                null,
                ct);
        }

        _db.SalesInvoiceDetails.Remove(detail);
        await _db.SaveChangesAsync(ct);

        var remaining = await _db.SalesInvoiceDetails.CountAsync(x => x.SalesInvoiceUid == invoice.Uid, ct);
        if (remaining == 0)
        {
            _db.SalesInvoices.Remove(invoice);
            await _db.SaveChangesAsync(ct);
        }
        else
        {
            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
        }

        await _audit.WriteAsync("Delete", "SalesInvoiceDetails", detailUid.ToString(),
            $"Deleted monthly entry line {detailUid}.", ct);

        return Ok(new MonthlyEntryApiResult { Success = true, Message = "Entry deleted." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateMonthlySalesInvoice(int customerUid, CancellationToken ct)
    {
        if (customerUid <= 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Select a monthly customer first." });
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == customerUid && x.IsActive, ct);
        if (customer is null || IsWalkInCustomer(customer.CustomerCode, customer.CustomerName))
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "Monthly customer was not found." });
        }

        var draftInvoices = await _db.SalesInvoices
            .Where(x => x.CustomerUid == customerUid
                        && x.Remarks != null && x.Remarks.StartsWith(MonthlyRemark)
                        && x.InvoiceStatus != "Cancelled")
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        if (draftInvoices.Count == 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "No item entry records to add." });
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        // Merge any legacy multi-draft invoices into the first draft (one invoice, many details).
        var primary = draftInvoices[0];
        if (draftInvoices.Count > 1)
        {
            for (var i = 1; i < draftInvoices.Count; i++)
            {
                var extra = draftInvoices[i];
                var extraDetails = await _db.SalesInvoiceDetails
                    .Where(x => x.SalesInvoiceUid == extra.Uid)
                    .ToListAsync(ct);
                foreach (var line in extraDetails)
                {
                    line.SalesInvoiceUid = primary.Uid;
                    if (string.IsNullOrWhiteSpace(line.Remarks) || !line.Remarks.StartsWith(MonthlyEntryDatePrefix))
                    {
                        line.Remarks = $"{MonthlyEntryDatePrefix}{extra.InvoiceDate:yyyy-MM-dd}";
                    }
                }

                _db.SalesInvoices.Remove(extra);
            }

            await _db.SaveChangesAsync(ct);
        }

        var detailCount = await _db.SalesInvoiceDetails.CountAsync(x => x.SalesInvoiceUid == primary.Uid, ct);
        if (detailCount == 0)
        {
            return BadRequest(new MonthlyEntryApiResult { Success = false, Message = "No item lines found to add." });
        }

        await RecalculateMonthlyDraftTotalsAsync(primary, ct);
        primary.CustomerUid = customer.Uid;
        primary.Remarks = $"Monthly POS â€” {customer.CustomerName}";
        primary.PaymentStatus = "Unpaid";
        primary.InvoiceStatus = "Completed";
        primary.UpdatedDate = DateTime.Now;
        if (primary.ClientTransactionId is null)
        {
            primary.ClientTransactionId = Guid.NewGuid();
        }
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("Update", "SalesInvoices", primary.Uid.ToString(),
            $"Finalized monthly sales invoice {primary.InvoiceNo} for {customer.CustomerName} with {detailCount} item(s).", ct);
        await tx.CommitAsync(ct);

        if (primary.ClientTransactionId is Guid finalizedTx)
        {
            await _syncOutbox.EnqueueSalesInvoiceAsync(primary.Uid, finalizedTx, ct);
        }

        return Ok(new MonthlyEntryApiResult
        {
            Success = true,
            Message = $"Sales invoice {primary.InvoiceNo} saved for {customer.CustomerName} with {detailCount} item(s).",
            SalesInvoiceUid = primary.Uid,
            InvoiceNo = primary.InvoiceNo
        });
    }

    private async Task RecalculateMonthlyDraftTotalsAsync(SalesInvoice invoice, CancellationToken ct)
    {
        var total = await _db.SalesInvoiceDetails
            .Where(x => x.SalesInvoiceUid == invoice.Uid)
            .SumAsync(x => (decimal?)x.LineTotal, ct) ?? 0m;

        invoice.SubTotal = total;
        invoice.NetAmount = total;
        invoice.DiscountAmount = 0;
        invoice.TaxAmount = 0;
        invoice.OtherCharges = 0;
        // Keep existing PaidAmount (advance / customer payments). Only refresh balance/status.
        await SyncInvoicePaidFromCustomerPaymentsAsync(invoice, ct);
    }

    /// <summary>
    /// Aligns invoice PaidAmount with linked CustomerPayments so monthly recalculation
    /// cannot wipe advances that were already applied.
    /// </summary>
    private async Task SyncInvoicePaidFromCustomerPaymentsAsync(SalesInvoice invoice, CancellationToken ct)
    {
        if (invoice.Uid <= 0)
        {
            invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
            invoice.PaymentStatus = invoice.PaidAmount <= 0
                ? "Unpaid"
                : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
            return;
        }

        var paidFromPayments = await _db.CustomerPayments.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == invoice.Uid)
            .SumAsync(x => (decimal?)x.AppliedToInvoiceAmount, ct) ?? 0m;

        // Prefer ledger total; never drop below what payments already recorded.
        invoice.PaidAmount = Math.Max(invoice.PaidAmount, paidFromPayments);
        if (invoice.NetAmount > 0 && invoice.PaidAmount > invoice.NetAmount)
            invoice.PaidAmount = invoice.NetAmount;

        invoice.BalanceAmount = Math.Max(0, invoice.NetAmount - invoice.PaidAmount);
        invoice.PaymentStatus = invoice.PaidAmount <= 0
            ? "Unpaid"
            : invoice.PaidAmount >= invoice.NetAmount ? "Paid" : "Partial";
    }

    /// <summary>
    /// Sets SalesInvoice.OtherCharges from CustomerMonthlySupply.DeliveryCharges (null → 0)
    /// once per detail line (each supply-day product line).
    /// </summary>
    private async Task ApplyMonthlySupplyDeliveryChargesAsync(SalesInvoice invoice, int customerUid, CancellationToken ct)
    {
        var detailProductUids = await _db.SalesInvoiceDetails.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == invoice.Uid)
            .Select(x => x.ProductUid)
            .ToListAsync(ct);

        if (detailProductUids.Count == 0)
        {
            invoice.OtherCharges = 0;
            invoice.NetAmount = invoice.SubTotal;
            invoice.BalanceAmount = invoice.NetAmount - invoice.PaidAmount;
            return;
        }

        var distinctProductUids = detailProductUids.Distinct().ToList();
        var chargeRows = await _db.CustomerMonthlySupplies.AsNoTracking()
            .Where(x => x.CustomerUid == customerUid
                        && x.ProductUid != null
                        && distinctProductUids.Contains(x.ProductUid.Value))
            .Select(x => new { ProductUid = x.ProductUid!.Value, x.DeliveryCharges })
            .ToListAsync(ct);

        var chargeByProduct = chargeRows
            .GroupBy(x => x.ProductUid)
            .ToDictionary(
                g => g.Key,
                g =>
                {
                    var raw = g.First().DeliveryCharges;
                    return raw is null || raw < 0 ? 0m : raw.Value;
                });

        var otherCharges = 0m;
        foreach (var productUid in detailProductUids)
        {
            if (chargeByProduct.TryGetValue(productUid, out var charge))
            {
                otherCharges += charge;
            }
        }

        invoice.OtherCharges = otherCharges;
        invoice.NetAmount = invoice.SubTotal + otherCharges;
        invoice.BalanceAmount = invoice.NetAmount - invoice.PaidAmount;
    }

    private static DateTime ParseEntryDate(string? remarks, DateTime fallback)
    {
        if (!string.IsNullOrWhiteSpace(remarks)
            && remarks.StartsWith(MonthlyEntryDatePrefix, StringComparison.OrdinalIgnoreCase)
            && DateTime.TryParse(remarks[MonthlyEntryDatePrefix.Length..], out var parsed))
        {
            return parsed.Date;
        }

        return fallback.Date;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay([FromBody] PosCheckoutRequest request, CancellationToken ct)
    {
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new PosCheckoutResultViewModel
            {
                Success = false,
                Message = "Basket is empty."
            });
        }

        var mode = string.Equals(request.Mode, "Monthly", StringComparison.OrdinalIgnoreCase)
            || string.Equals(request.Mode, "Credit", StringComparison.OrdinalIgnoreCase)
            ? "Monthly"
            : "Cash";
        var productIds = request.Items.Select(x => x.ProductUid).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Uid) && x.IsActive)
            .ToDictionaryAsync(x => x.Uid, ct);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new PosCheckoutResultViewModel
            {
                Success = false,
                Message = "One or more products are invalid or inactive."
            });
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest(new PosCheckoutResultViewModel
                {
                    Success = false,
                    Message = "Quantity must be greater than zero."
                });
            }
        }

        var paymentMethodUid = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => mode == "Cash" && x.PaymentName.Contains("Cash"))
            .ThenBy(x => x.PaymentName)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var branchUid = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        int? userUid = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var parsedUserId))
        {
            userUid = parsedUserId;
        }

        var clientTxId = request.ClientTransactionId is Guid g && g != Guid.Empty
            ? g
            : Guid.NewGuid();

        var existingByClient = await _db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.ClientTransactionId == clientTxId, ct);
        if (existingByClient is not null)
        {
            return Ok(new PosCheckoutResultViewModel
            {
                Success = true,
                Message = $"Sale already saved as {existingByClient.InvoiceNo}.",
                InvoiceUid = existingByClient.Uid,
                InvoiceNo = existingByClient.InvoiceNo,
                NetAmount = existingByClient.NetAmount,
                InvoiceDate = existingByClient.InvoiceDate
            });
        }

        var invoiceNo = $"POS-{mode[0]}-{DateTime.Now:yyyyMMddHHmmssfff}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
        {
            invoiceNo = $"POS-{mode[0]}-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";
        }

        var subTotal = request.Items.Sum(x => Math.Round(x.UnitPrice * x.Quantity, 2));
        var isCash = mode == "Cash";

        int? customerUid = null;
        if (isCash)
        {
            customerUid = await ResolveWalkInCustomerUidAsync(ct);
            if (customerUid is null)
            {
                return BadRequest(new PosCheckoutResultViewModel
                {
                    Success = false,
                    Message = "Walk-in Customer was not found. Please create an active customer named Walk-in Customer."
                });
            }
        }

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var sessionBranch = HttpContext.Session.GetString(AccountController.SessionBranchKey);
        var sessionTerminal = HttpContext.Session.GetString(AccountController.SessionTerminalNameKey);
        if (string.IsNullOrWhiteSpace(sessionBranch))
            sessionBranch = User.FindFirstValue("Branch");
        if (string.IsNullOrWhiteSpace(sessionTerminal))
            sessionTerminal = User.FindFirstValue("TerminalName");

        var invoice = new SalesInvoice
        {
            ClientTransactionId = clientTxId,
            InvoiceNo = invoiceNo,
            InvoiceDate = DateTime.Now,
            BranchUid = branchUid,
            Branch = string.IsNullOrWhiteSpace(sessionBranch) ? null : sessionBranch.Trim(),
            CustomerUid = customerUid,
            UserUid = userUid,
            PaymentMethodUid = paymentMethodUid,
            Terminal = string.IsNullOrWhiteSpace(sessionTerminal) ? null : sessionTerminal.Trim(),
            SubTotal = subTotal,
            DiscountAmount = 0,
            TaxAmount = 0,
            OtherCharges = 0,
            NetAmount = subTotal,
            PaidAmount = isCash ? subTotal : 0,
            BalanceAmount = isCash ? 0 : subTotal,
            PaymentStatus = isCash ? "Paid" : "Unpaid",
            InvoiceStatus = "Completed",
            Remarks = $"{mode} POS sale",
            CreatedDate = DateTime.Now
        };

        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        var stockLines = new List<InventoryLineMovement>();
        foreach (var item in request.Items)
        {
            var product = products[item.ProductUid];
            var lineTotal = Math.Round(item.UnitPrice * item.Quantity, 2);
            _db.SalesInvoiceDetails.Add(new SalesInvoiceDetail
            {
                SalesInvoiceUid = invoice.Uid,
                ProductUid = product.Uid,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                Quantity = item.Quantity,
                UnitPrice = item.UnitPrice,
                DiscountPercent = 0,
                DiscountAmount = 0,
                TaxPercent = product.TaxPercent,
                TaxAmount = 0,
                LineTotal = lineTotal,
                CostPrice = product.PurchasePrice
            });
            stockLines.Add(new InventoryLineMovement
            {
                ProductUid = product.Uid,
                ProductName = product.ProductName,
                Quantity = item.Quantity,
                UnitCost = item.UnitPrice
            });
        }

        await _db.SaveChangesAsync(ct);

        try
        {
            var stockResult = await _inventory.ApplySaleAsync(
                invoice.Uid, invoice.InvoiceNo, stockLines, warehouseUid: null, branchUid, userUid, terminalUid: null, ct);
            if (stockResult.HasShortages)
            {
                invoice.Remarks = _inventory.MergeShortageRemarks(invoice.Remarks, stockResult);
                await _db.SaveChangesAsync(ct);
            }
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new PosCheckoutResultViewModel
            {
                Success = false,
                Message = ex.Message
            });
        }

        await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(),
            $"POS {mode} sale {invoice.InvoiceNo} amount {invoice.NetAmount:N2}", ct);
        await tx.CommitAsync(ct);

        await _syncOutbox.EnqueueSalesInvoiceAsync(invoice.Uid, clientTxId, ct);

        return Ok(new PosCheckoutResultViewModel
        {
            Success = true,
            Message = $"Sale saved as {invoice.InvoiceNo}.",
            InvoiceUid = invoice.Uid,
            InvoiceNo = invoice.InvoiceNo,
            NetAmount = invoice.NetAmount,
            InvoiceDate = invoice.InvoiceDate
        });
    }

    private async Task<IActionResult> SalePage(string mode, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        ViewData["Title"] = $"{mode} POS";

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.ProductName)
            .Select(x => new PosProductItemViewModel
            {
                Uid = x.Uid,
                ProductCode = x.ProductCode,
                ProductName = x.ProductName,
                Barcode = x.Barcode,
                SalePrice = x.SalePrice,
                CategoryUid = x.CategoryUid,
                ImagePath = x.ImagePath
            })
            .ToListAsync(ct);

        var categories = await _db.ProductCategories.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.CategoryName)
            .Select(x => new PosCategoryItemViewModel
            {
                Uid = x.Uid,
                CategoryName = x.CategoryName
            })
            .ToListAsync(ct);

        var categoryNames = categories.ToDictionary(x => x.Uid, x => x.CategoryName);
        foreach (var product in products)
        {
            if (product.CategoryUid is int categoryUid
                && categoryNames.TryGetValue(categoryUid, out var categoryName))
            {
                product.CategoryName = categoryName;
            }
        }

        var layout = await _db.Configurations.AsNoTracking()
            .Where(x => x.ConfigKey == SetupController.PosItemLayoutKey)
            .Select(x => x.ConfigValue)
            .FirstOrDefaultAsync(ct);

        return View("Sale", new PosSaleViewModel
        {
            Mode = mode,
            UseCardLayout = SetupController.IsCardLayout(layout),
            Products = products,
            Categories = categories
        });
    }

    private async Task<int?> ResolveWalkInCustomerUidAsync(CancellationToken ct)
    {
        return await _db.Customers.AsNoTracking()
            .Where(x => x.IsActive &&
                        (x.CustomerCode == "C001" ||
                         x.CustomerName.Contains("Walk-in") ||
                         x.CustomerName.Contains("Walk-In") ||
                         x.CustomerName.Contains("Walk In")))
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);
    }

    private static bool IsWalkInCustomer(string? customerCode, string? customerName)
        => Services.CustomerClassification.IsWalkIn(customerCode, customerName);
}
