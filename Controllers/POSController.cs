using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    [HttpGet]
    public async Task<IActionResult> Credit(CancellationToken ct)
    {
        return await SalePage("Credit", ct);
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

        var mode = string.Equals(request.Mode, "Credit", StringComparison.OrdinalIgnoreCase) ? "Credit" : "Cash";
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
                SalePrice = x.SalePrice
            })
            .ToListAsync(ct);

        return View("Sale", new PosSaleViewModel
        {
            Mode = mode,
            Products = products
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
}
