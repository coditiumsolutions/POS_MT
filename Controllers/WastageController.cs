using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

/// <summary>
/// Wastage write-off: same basket UX as Item Return, but deducts stock and posts a sales invoice for customer "Wastage".
/// </summary>
[Authorize(Policy = "Staff")]
public class WastageController : Controller
{
    public const string WastageStatus = "Wastage";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly IInventoryLedgerService _inventory;
    private readonly INavContextService _navContext;

    public WastageController(
        POSDbContext db,
        IAuditService audit,
        IInventoryLedgerService inventory,
        INavContextService navContext)
    {
        _db = db;
        _audit = audit;
        _inventory = inventory;
        _navContext = navContext;
    }

    private void EnsureNav() => _navContext.SetArea("Adjustments");

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureNav();
        ViewData["Title"] = "Wastage";

        var list = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.InvoiceStatus == WastageStatus)
            .OrderByDescending(x => x.InvoiceDate)
            .ThenByDescending(x => x.Uid)
            .Select(x => new SalesReturnListItemViewModel
            {
                Uid = x.Uid,
                ReturnNo = x.InvoiceNo,
                ReturnDate = x.InvoiceDate,
                NetAmount = x.NetAmount,
                Remarks = x.Remarks
            })
            .ToListAsync(ct);

        return View(list);
    }

    public async Task<IActionResult> Create(CancellationToken ct)
    {
        EnsureNav();
        ViewData["Title"] = "New Wastage";

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

        return View(new SalesReturnCreateViewModel
        {
            UseCardLayout = SetupController.IsCardLayout(layout),
            Products = products,
            Categories = categories
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveWastage([FromBody] SalesReturnCheckoutRequest request, CancellationToken ct)
    {
        EnsureNav();
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new SalesReturnCheckoutResultViewModel
            {
                Success = false,
                Message = "Add at least one product for wastage."
            });
        }

        var productIds = request.Items.Select(x => x.ProductUid).Distinct().ToList();
        var products = await _db.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Uid) && x.IsActive)
            .ToDictionaryAsync(x => x.Uid, ct);

        if (products.Count != productIds.Count)
        {
            return BadRequest(new SalesReturnCheckoutResultViewModel
            {
                Success = false,
                Message = "One or more products are invalid or inactive."
            });
        }

        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0)
            {
                return BadRequest(new SalesReturnCheckoutResultViewModel
                {
                    Success = false,
                    Message = "Quantity must be greater than zero."
                });
            }

            if (item.UnitPrice < 0)
            {
                return BadRequest(new SalesReturnCheckoutResultViewModel
                {
                    Success = false,
                    Message = "Unit price cannot be negative."
                });
            }
        }

        var wastageCustomerUid = await EnsureWastageCustomerUidAsync(ct);

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
            userUid = parsedUserId;
        var userName = User.Identity?.Name
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? User.FindFirstValue("UserName");
        if (string.IsNullOrWhiteSpace(userName) && userUid is int uid)
        {
            userName = await _db.Users.AsNoTracking()
                .Where(u => u.Uid == uid)
                .Select(u => u.UserName)
                .FirstOrDefaultAsync(ct);
        }

        var sessionBranch = HttpContext.Session.GetString(AccountController.SessionBranchKey)
                            ?? User.FindFirstValue("Branch");
        var sessionTerminal = HttpContext.Session.GetString(AccountController.SessionTerminalNameKey)
                              ?? User.FindFirstValue("TerminalName");

        int? branchUid = null;
        if (!string.IsNullOrWhiteSpace(sessionBranch))
        {
            var branchName = sessionBranch.Trim();
            branchUid = await _db.Branches.AsNoTracking()
                .Where(b => b.IsActive && b.BranchName == branchName)
                .Select(b => (int?)b.Uid)
                .FirstOrDefaultAsync(ct);
        }

        if (branchUid is null or <= 0)
        {
            branchUid = await _db.Branches.AsNoTracking()
                .Where(x => x.IsActive)
                .OrderBy(x => x.Uid)
                .Select(x => (int?)x.Uid)
                .FirstOrDefaultAsync(ct);
        }

        int? terminalUid = null;
        if (!string.IsNullOrWhiteSpace(sessionTerminal))
        {
            var terminalName = sessionTerminal.Trim();
            terminalUid = await _db.Terminals.AsNoTracking()
                .Where(t => t.IsActive && t.TerminalName == terminalName)
                .Select(t => (int?)t.Uid)
                .FirstOrDefaultAsync(ct);
        }

        var paymentMethodUid = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.PaymentName.Contains("Cash"))
            .ThenBy(x => x.PaymentName)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var netAmount = request.Items.Sum(x => Math.Round(x.UnitPrice * x.Quantity, 2));

        var invoiceNo = $"WAS-{DateTime.Now:yyyyMMddHHmmssfff}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
            invoiceNo = $"WAS-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";

        var remarksParts = new List<string> { "Wastage" };
        if (!string.IsNullOrWhiteSpace(request.Reason))
            remarksParts.Add(request.Reason.Trim());
        var remarks = string.Join(" | ", remarksParts);
        if (remarks.Length > 500)
            remarks = remarks[..497] + "...";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var invoice = new SalesInvoice
        {
            ClientTransactionId = Guid.NewGuid(),
            InvoiceNo = invoiceNo,
            InvoiceDate = DateTime.Now,
            BranchUid = branchUid,
            Branch = string.IsNullOrWhiteSpace(sessionBranch) ? null : sessionBranch.Trim(),
            CustomerUid = wastageCustomerUid,
            UserUid = userUid,
            UserName = string.IsNullOrWhiteSpace(userName) ? null : userName.Trim(),
            PaymentMethodUid = paymentMethodUid,
            TerminalUid = terminalUid,
            Terminal = string.IsNullOrWhiteSpace(sessionTerminal) ? null : sessionTerminal.Trim(),
            SubTotal = netAmount,
            DiscountAmount = 0,
            TaxAmount = 0,
            OtherCharges = 0,
            NetAmount = netAmount,
            PaidAmount = netAmount,
            BalanceAmount = 0,
            PaymentStatus = "Paid",
            InvoiceStatus = WastageStatus,
            Remarks = remarks,
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
                TaxPercent = 0,
                TaxAmount = 0,
                LineTotal = lineTotal,
                CostPrice = product.PurchasePrice,
                Remarks = "Wastage"
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
                invoice.Uid, invoice.InvoiceNo, stockLines,
                warehouseUid: null, branchUid, userUid, terminalUid, ct);
            if (stockResult.HasShortages)
                invoice.Remarks = _inventory.MergeShortageRemarks(invoice.Remarks, stockResult);
            await _db.SaveChangesAsync(ct);
        }
        catch (InvalidOperationException ex)
        {
            await tx.RollbackAsync(ct);
            return BadRequest(new SalesReturnCheckoutResultViewModel
            {
                Success = false,
                Message = ex.Message
            });
        }

        await _audit.WriteAsync("Create", "Wastage", invoice.Uid.ToString(),
            $"Wastage {invoice.InvoiceNo} amount {invoice.NetAmount:N2}", ct);
        await tx.CommitAsync(ct);

        return Ok(new SalesReturnCheckoutResultViewModel
        {
            Success = true,
            Message = $"Wastage saved as {invoice.InvoiceNo}. Stock deducted. Sales invoice customer: Wastage.",
            InvoiceUid = invoice.Uid,
            ReturnNo = invoice.InvoiceNo,
            NetAmount = invoice.NetAmount
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureNav();
        var invoice = await _db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == id && x.InvoiceStatus == WastageStatus, ct);
        if (invoice is null)
            return NotFound();

        var lines = await _db.SalesInvoiceDetails.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == id)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        ViewData["Title"] = $"Wastage {invoice.InvoiceNo}";
        ViewBag.Lines = lines;
        return View(invoice);
    }

    private async Task<int> EnsureWastageCustomerUidAsync(CancellationToken ct)
    {
        var existing = await _db.Customers
            .Where(x => x.CustomerName == "Wastage" || x.CustomerCode == "WASTAGE")
            .OrderBy(x => x.Uid)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
        {
            if (!existing.IsActive)
            {
                existing.IsActive = true;
                existing.UpdatedDate = DateTime.Now;
                await _db.SaveChangesAsync(ct);
            }

            return existing.Uid;
        }

        var customer = new Customer
        {
            CustomerCode = "WASTAGE",
            CustomerName = "Wastage",
            IsActive = true,
            OpeningBalance = 0,
            AdvanceBalance = 0,
            CreditLimit = 0,
            DiscountPercent = 0,
            CreatedDate = DateTime.Now
        };
        _db.Customers.Add(customer);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Customers", customer.Uid.ToString(),
            "Auto-created Wastage customer for wastage write-offs", ct);
        return customer.Uid;
    }
}
