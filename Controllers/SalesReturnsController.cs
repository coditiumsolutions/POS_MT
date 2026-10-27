using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "Staff")]
public class SalesReturnsController : Controller
{
    public const string ReturnStatus = "Return";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly IInventoryLedgerService _inventory;
    private readonly INavContextService _navContext;

    public SalesReturnsController(
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

    private void EnsureNav() => _navContext.SetArea("AllModules");

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        EnsureNav();
        ViewData["Title"] = "Sales Returns";

        var list = await _db.SalesInvoices.AsNoTracking()
            .Where(x => x.InvoiceStatus == ReturnStatus)
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
        ViewData["Title"] = "New Sales Return";

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
    public async Task<IActionResult> SaveReturn([FromBody] SalesReturnCheckoutRequest request, CancellationToken ct)
    {
        EnsureNav();
        if (request is null || request.Items is null || request.Items.Count == 0)
        {
            return BadRequest(new SalesReturnCheckoutResultViewModel
            {
                Success = false,
                Message = "Add at least one product to return."
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

        var walkInUid = await _db.Customers.AsNoTracking()
            .Where(x => x.IsActive &&
                        (x.CustomerCode == "C001" ||
                         x.CustomerName.Contains("Walk-in") ||
                         x.CustomerName.Contains("Walk-In") ||
                         x.CustomerName.Contains("Walk In")))
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        if (walkInUid is null)
        {
            return BadRequest(new SalesReturnCheckoutResultViewModel
            {
                Success = false,
                Message = "Walk-in Customer was not found. Please create an active Walk-in customer."
            });
        }

        var branchUid = await _db.Branches.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        int? userUid = null;
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var parsedUserId))
            userUid = parsedUserId;

        var sessionBranch = HttpContext.Session.GetString(AccountController.SessionBranchKey);
        var sessionTerminal = HttpContext.Session.GetString(AccountController.SessionTerminalNameKey);
        if (string.IsNullOrWhiteSpace(sessionBranch))
            sessionBranch = User.FindFirstValue("Branch");
        if (string.IsNullOrWhiteSpace(sessionTerminal))
            sessionTerminal = User.FindFirstValue("TerminalName");

        var returnValue = request.Items.Sum(x => Math.Round(x.UnitPrice * x.Quantity, 2));
        var netAmount = -Math.Abs(returnValue);

        var returnNo = $"RET-{DateTime.Now:yyyyMMddHHmmssfff}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == returnNo, ct))
            returnNo = $"RET-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";

        var remarksParts = new List<string> { "Sales return" };
        if (!string.IsNullOrWhiteSpace(request.OriginalInvoiceNo))
            remarksParts.Add($"Original: {request.OriginalInvoiceNo.Trim()}");
        if (!string.IsNullOrWhiteSpace(request.Reason))
            remarksParts.Add(request.Reason.Trim());
        var remarks = string.Join(" | ", remarksParts);
        if (remarks.Length > 500)
            remarks = remarks[..497] + "...";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);

        var invoice = new SalesInvoice
        {
            InvoiceNo = returnNo,
            InvoiceDate = DateTime.Now,
            BranchUid = branchUid,
            Branch = string.IsNullOrWhiteSpace(sessionBranch) ? null : sessionBranch.Trim(),
            CustomerUid = walkInUid,
            UserUid = userUid,
            Terminal = string.IsNullOrWhiteSpace(sessionTerminal) ? null : sessionTerminal.Trim(),
            SubTotal = netAmount,
            DiscountAmount = 0,
            TaxAmount = 0,
            OtherCharges = 0,
            NetAmount = netAmount,
            PaidAmount = netAmount,
            BalanceAmount = 0,
            PaymentStatus = "Refunded",
            InvoiceStatus = ReturnStatus,
            Remarks = remarks,
            CreatedDate = DateTime.Now
        };

        _db.SalesInvoices.Add(invoice);
        await _db.SaveChangesAsync(ct);

        var stockLines = new List<InventoryLineMovement>();
        foreach (var item in request.Items)
        {
            var product = products[item.ProductUid];
            var lineTotal = -Math.Abs(Math.Round(item.UnitPrice * item.Quantity, 2));
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
            await _inventory.ApplyReturnAsync(
                invoice.Uid, invoice.InvoiceNo, stockLines,
                warehouseUid: null, branchUid, userUid, terminalUid: null, ct);
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

        await _audit.WriteAsync("Create", "SalesReturns", invoice.Uid.ToString(),
            $"Sales return {invoice.InvoiceNo} amount {invoice.NetAmount:N2}", ct);
        await tx.CommitAsync(ct);

        return Ok(new SalesReturnCheckoutResultViewModel
        {
            Success = true,
            Message = $"Return saved as {invoice.InvoiceNo}. Stock restored and sales total adjusted.",
            InvoiceUid = invoice.Uid,
            ReturnNo = invoice.InvoiceNo,
            NetAmount = invoice.NetAmount
        });
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        EnsureNav();
        var invoice = await _db.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == id && x.InvoiceStatus == ReturnStatus, ct);
        if (invoice is null)
            return NotFound();

        var lines = await _db.SalesInvoiceDetails.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == id)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        ViewData["Title"] = $"Return {invoice.InvoiceNo}";
        ViewBag.Lines = lines;
        return View(invoice);
    }
}
