using System.Security.Claims;
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
public class POSController : Controller
{
    private readonly INavContextService _navContext;
    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public POSController(INavContextService navContext, POSDbContext db, IAuditService audit)
    {
        _navContext = navContext;
        _db = db;
        _audit = audit;
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

    [HttpGet]
    public async Task<IActionResult> CustomerMonthlyItems(int customerUid, CancellationToken ct)
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

        var items = await _db.CustomerMonthlyItems.AsNoTracking()
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
                UnitPrice = x.UnitPrice
            })
            .ToListAsync(ct);

        return Json(items);
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

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        foreach (var customer in customers)
        {
            if (IsWalkInCustomer(customer.CustomerCode, customer.CustomerName))
            {
                skipped.Add($"{customer.CustomerName} (walk-in)");
                continue;
            }

            var monthlyItems = await _db.CustomerMonthlyItems.AsNoTracking()
                .Where(x => x.CustomerUid == customer.Uid)
                .OrderBy(x => x.ProductName)
                .ToListAsync(ct);

            if (monthlyItems.Count == 0)
            {
                skipped.Add($"{customer.CustomerName} (no monthly items)");
                continue;
            }

            var productUids = monthlyItems
                .Select(x => x.ProductUid)
                .Where(x => x is > 0)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();

            var products = productUids.Count == 0
                ? new Dictionary<int, Product>()
                : await _db.Products.AsNoTracking()
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
                    Remarks = $"{MonthlySupplyRemarkPrefix} — {customer.CustomerCode} — {customer.CustomerName} — {supplyDay:yyyy-MM}",
                    CreatedDate = DateTime.Now
                };

                _db.SalesInvoices.Add(invoice);
                await _db.SaveChangesAsync(ct);
                createdNew = true;
            }

            foreach (var line in monthlyItems)
            {
                products.TryGetValue(line.ProductUid ?? 0, out var product);
                var quantity = line.Quantity < 0.1m ? 0.1m : line.Quantity;
                var unitPrice = line.UnitPrice;
                var lineTotal = Math.Round(unitPrice * quantity, 2);

                _db.SalesInvoiceDetails.Add(new SalesInvoiceDetail
                {
                    SalesInvoiceUid = invoice.Uid,
                    ProductUid = line.ProductUid ?? product?.Uid ?? 0,
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
                    Remarks = $"{MonthlyEntryDatePrefix}{supplyDay:yyyy-MM-dd}"
                });
            }

            await _db.SaveChangesAsync(ct);
            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            invoice.UpdatedDate = DateTime.Now;
            invoice.UserUid ??= userUid;
            await _db.SaveChangesAsync(ct);

            if (createdNew)
            {
                await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(),
                    $"Monthly supply invoice {invoice.InvoiceNo} for {customer.CustomerCode} ({supplyDay:yyyy-MM}) with {monthlyItems.Count} detail line(s).", ct);
                processed.Add($"{customer.CustomerCode}: created {invoice.InvoiceNo} (+{monthlyItems.Count} lines)");
            }
            else
            {
                await _audit.WriteAsync("Update", "SalesInvoices", invoice.Uid.ToString(),
                    $"Added {monthlyItems.Count} monthly supply detail line(s) to {invoice.InvoiceNo} for {customer.CustomerCode} on {supplyDay:yyyy-MM-dd}.", ct);
                processed.Add($"{customer.CustomerCode}: updated {invoice.InvoiceNo} (+{monthlyItems.Count} lines)");
            }
        }

        await tx.CommitAsync(ct);

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
    public async Task<IActionResult> MonthlySupplyItems(string? customerUids, int? activeCustomerUid, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        ViewData["Title"] = "Monthly Supply Items";

        var uidList = ParseCustomerUids(customerUids);
        if (uidList.Count == 0)
        {
            TempData["Error"] = "Select at least one customer.";
            return RedirectToAction(nameof(MonthlySupply));
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
            return RedirectToAction(nameof(MonthlySupply));
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
            Text = string.IsNullOrWhiteSpace(p.BrandName)
                ? $"{p.ProductCode} — {p.ProductName}"
                : $"{p.ProductCode} — {p.ProductName} ({p.BrandName})"
        }).ToList();

        var items = await _db.CustomerMonthlyItems.AsNoTracking()
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
                UnitPrice = x.UnitPrice
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
            Items = items
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMonthlySupplyItem(MonthlySupplyAddItemRequest request, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        var redirectUids = request.CustomerUids;

        if (!ModelState.IsValid || request.CustomerUid <= 0 || request.ProductUid <= 0)
        {
            TempData["Error"] = "Select a customer and product, then enter quantity and price.";
            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = request.CustomerUid });
        }

        var customer = await _db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.CustomerUid && x.IsActive, ct);
        if (customer is null)
        {
            TempData["Error"] = "Customer was not found.";
            return RedirectToAction(nameof(MonthlySupply));
        }

        var product = await _db.Products.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == request.ProductUid && x.IsActive, ct);
        if (product is null)
        {
            TempData["Error"] = "Product was not found.";
            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = request.CustomerUid });
        }

        var unitPrice = request.UnitPrice > 0 ? request.UnitPrice : product.SalePrice;
        var quantity = request.Quantity < 0.1m ? 0.1m : request.Quantity;

        _db.CustomerMonthlyItems.Add(new CustomerMonthlyItem
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
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "CustomerMonthlyItems", customer.Uid.ToString(),
            $"Added monthly supply item {product.ProductCode} for {customer.CustomerCode}", ct);

        TempData["Success"] = $"Added {product.ProductName} for {customer.CustomerName}.";
        return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids = redirectUids, activeCustomerUid = customer.Uid });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMonthlySupplyItem(MonthlySupplyUpdateItemRequest request, CancellationToken ct)
    {
        _navContext.SetArea("POS");
        if (!ModelState.IsValid || request.Id <= 0 || request.CustomerUid <= 0)
        {
            TempData["Error"] = "Enter valid quantity and unit price.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = request.CustomerUid });
        }

        var row = await _db.CustomerMonthlyItems.FirstOrDefaultAsync(x => x.Uid == request.Id, ct);
        if (row is null || row.CustomerUid != request.CustomerUid)
        {
            TempData["Error"] = "Item was not found for this customer.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = request.CustomerUid });
        }

        var quantity = request.Quantity < 0.1m ? 0.1m : request.Quantity;
        row.Quantity = quantity;
        row.UnitPrice = request.UnitPrice < 0 ? 0 : request.UnitPrice;
        row.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Update", "CustomerMonthlyItems", row.Uid.ToString(),
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
        CancellationToken ct = default)
    {
        _navContext.SetArea("POS");
        var row = await _db.CustomerMonthlyItems.FirstOrDefaultAsync(x => x.Uid == id, ct);
        if (row is null)
        {
            TempData["Error"] = "Item was not found.";
            if (fromMonthlySupply)
            {
                return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid });
            }

            return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids, activeCustomerUid });
        }

        if (fromMonthlySupply && customerUid.HasValue && row.CustomerUid != customerUid)
        {
            TempData["Error"] = "Item does not belong to this customer.";
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid });
        }

        var ownerCustomerUid = row.CustomerUid;
        _db.CustomerMonthlyItems.Remove(row);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Delete", "CustomerMonthlyItems", id.ToString(),
            $"Deleted monthly supply item {row.ProductCode} for {row.CustomerCode}", ct);

        TempData["Success"] = "Item deleted.";
        if (fromMonthlySupply)
        {
            return RedirectToAction(nameof(MonthlySupply), new { openCustomer = customerUid ?? ownerCustomerUid });
        }

        return RedirectToAction(nameof(MonthlySupplyItems), new { customerUids, activeCustomerUid = activeCustomerUid ?? ownerCustomerUid });
    }

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
                  && inv.Remarks == MonthlyRemark
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
                && x.Remarks == MonthlyRemark
                && x.InvoiceStatus != "Cancelled", ct);
            if (invoice is null)
            {
                return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Draft invoice not found." });
            }

            detail.ProductUid = product.Uid;
            detail.ProductCode = product.ProductCode;
            detail.ProductName = product.ProductName;
            detail.Quantity = request.Quantity;
            detail.UnitPrice = request.UnitPrice;
            detail.LineTotal = lineTotal;
            detail.CostPrice = product.PurchasePrice;
            detail.TaxPercent = product.TaxPercent;
            detail.Remarks = $"{MonthlyEntryDatePrefix}{entryDate:yyyy-MM-dd}";

            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Update", "SalesInvoiceDetails", detail.Uid.ToString(),
                $"Updated monthly line for {customer.CustomerName}: {product.ProductName}", ct);
        }
        else
        {
            invoice = await _db.SalesInvoices.FirstOrDefaultAsync(x =>
                x.CustomerUid == customer.Uid
                && x.Remarks == MonthlyRemark
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

            await RecalculateMonthlyDraftTotalsAsync(invoice, ct);
            invoice.UpdatedDate = DateTime.Now;
            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "SalesInvoiceDetails", detail.Uid.ToString(),
                $"Monthly line for {customer.CustomerName}: {product.ProductName}", ct);
        }

        await tx.CommitAsync(ct);

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
            && x.Remarks == MonthlyRemark
            && x.InvoiceStatus != "Cancelled", ct);
        if (invoice is null)
        {
            return NotFound(new MonthlyEntryApiResult { Success = false, Message = "Draft invoice not found." });
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
                        && x.Remarks == MonthlyRemark
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
        primary.Remarks = $"Monthly POS — {customer.CustomerName}";
        primary.PaymentStatus = "Unpaid";
        primary.InvoiceStatus = "Completed";
        primary.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(ct);

        await _audit.WriteAsync("Update", "SalesInvoices", primary.Uid.ToString(),
            $"Finalized monthly sales invoice {primary.InvoiceNo} for {customer.CustomerName} with {detailCount} item(s).", ct);
        await tx.CommitAsync(ct);

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
        invoice.PaidAmount = 0;
        invoice.BalanceAmount = total;
        invoice.PaymentStatus = "Unpaid";
        invoice.DiscountAmount = 0;
        invoice.TaxAmount = 0;
        invoice.OtherCharges = 0;
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

        var invoice = new SalesInvoice
        {
            InvoiceNo = invoiceNo,
            InvoiceDate = DateTime.Now,
            BranchUid = branchUid,
            CustomerUid = customerUid,
            UserUid = userUid,
            PaymentMethodUid = paymentMethodUid,
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
        }

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(),
            $"POS {mode} sale {invoice.InvoiceNo} amount {invoice.NetAmount:N2}", ct);
        await tx.CommitAsync(ct);

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
                CategoryUid = x.CategoryUid
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

        return View("Sale", new PosSaleViewModel
        {
            Mode = mode,
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
