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
/// Convert milk → Fresh Yogurt (40 kg milk : 32 kg yogurt).
/// Deducts milk, adds yogurt, and posts a sales invoice for the production.
/// </summary>
[Authorize(Policy = "Staff")]
public class YogurtProductionController : Controller
{
    /// <summary>Recipe: 40 kg milk produces 32 kg yogurt.</summary>
    public const decimal RecipeMilkKg = 40m;
    public const decimal RecipeYogurtKg = 32m;

    private const string TxProductionOut = "ProductionOut";
    private const string TxProductionIn = "ProductionIn";
    private const string ProductionRemarkPrefix = "Yogurt production";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;
    private readonly IInventoryLedgerService _inventory;
    private readonly INavContextService _navContext;

    public YogurtProductionController(
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

    public static decimal MilkForYogurt(decimal yogurtKg)
        => Math.Round(yogurtKg * RecipeMilkKg / RecipeYogurtKg, 3);

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        _navContext.SetArea("Purchases");
        ViewData["Title"] = "Yogurt Production";
        var model = await BuildPageModelAsync(0, ct);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Produce(YogurtProductionViewModel model, CancellationToken ct)
    {
        _navContext.SetArea("Purchases");
        ViewData["Title"] = "Yogurt Production";

        if (model.YogurtQuantity <= 0)
            ModelState.AddModelError(nameof(model.YogurtQuantity), "Enter Fresh Yogurt quantity (kg).");

        var page = await BuildPageModelAsync(model.YogurtQuantity, ct);
        if (page.MilkProductUid is null || page.YogurtProductUid is null)
        {
            ModelState.AddModelError(string.Empty,
                "Products 'Fresh Milk' and 'Fresh Yogurt' must exist and be active.");
            return View("Index", page);
        }

        var yogurtQty = Math.Round(model.YogurtQuantity, 3);
        var milkQty = MilkForYogurt(yogurtQty);
        page.YogurtQuantity = yogurtQty;
        page.MilkQuantity = milkQty;

        if (!ModelState.IsValid)
            return View("Index", page);

        var (branchName, branchUid) = await ResolveSessionBranchAsync(ct);
        if (string.IsNullOrWhiteSpace(branchName) || branchUid is null or <= 0)
        {
            ModelState.AddModelError(string.Empty, "Branch is required. Assign a Branch to your user.");
            return View("Index", page);
        }

        var (terminalName, terminalUid) = await ResolveSessionTerminalAsync(ct);
        if (string.IsNullOrWhiteSpace(terminalName))
        {
            ModelState.AddModelError(string.Empty, "Terminal is required. Assign a Terminal to your user.");
            return View("Index", page);
        }

        // Milk may go negative (e.g. available 10 − needed 40 = −30).
        var shopCustomerUid = await EnsureShopCustomerUidAsync(ct);

        int? userUid = null;
        if (int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var parsedUserId))
            userUid = parsedUserId;

        var warehouseUid = await _inventory.ResolveWarehouseUidAsync(null, branchUid, ct);
        var milk = await _db.Products.AsNoTracking().FirstAsync(x => x.Uid == page.MilkProductUid, ct);
        var yogurt = await _db.Products.AsNoTracking().FirstAsync(x => x.Uid == page.YogurtProductUid, ct);

        var paymentMethodUid = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.PaymentName.Contains("Cash"))
            .ThenBy(x => x.PaymentName)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var yogurtLineTotal = Math.Round(yogurtQty * yogurt.SalePrice, 2);
        var invoiceNo = $"YOG-{DateTime.Now:yyyyMMddHHmmssfff}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
            invoiceNo = $"YOG-{DateTime.Now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            var now = DateTime.Now;
            var remarks = $"{ProductionRemarkPrefix}: {milkQty:0.###} kg {milk.ProductName} → {yogurtQty:0.###} kg {yogurt.ProductName} (40:32)";

            var invoice = new SalesInvoice
            {
                ClientTransactionId = Guid.NewGuid(),
                InvoiceNo = invoiceNo,
                InvoiceDate = now,
                BranchUid = branchUid,
                Branch = branchName,
                CustomerUid = shopCustomerUid,
                UserUid = userUid,
                PaymentMethodUid = paymentMethodUid,
                TerminalUid = terminalUid,
                Terminal = terminalName,
                SubTotal = yogurtLineTotal,
                DiscountAmount = 0,
                TaxAmount = 0,
                OtherCharges = 0,
                NetAmount = yogurtLineTotal,
                PaidAmount = yogurtLineTotal,
                BalanceAmount = 0,
                PaymentStatus = "Paid",
                InvoiceStatus = "Completed",
                Remarks = remarks,
                CreatedDate = now
            };
            _db.SalesInvoices.Add(invoice);
            await _db.SaveChangesAsync(ct);

            _db.SalesInvoiceDetails.Add(new SalesInvoiceDetail
            {
                SalesInvoiceUid = invoice.Uid,
                ProductUid = yogurt.Uid,
                ProductCode = yogurt.ProductCode,
                ProductName = yogurt.ProductName,
                Quantity = yogurtQty,
                UnitPrice = yogurt.SalePrice,
                DiscountPercent = 0,
                DiscountAmount = 0,
                TaxPercent = 0,
                TaxAmount = 0,
                LineTotal = yogurtLineTotal,
                CostPrice = yogurt.PurchasePrice,
                Remarks = $"Produced from {milkQty:0.###} kg milk"
            });

            // Document milk consumed on the same invoice (qty only; no amount).
            _db.SalesInvoiceDetails.Add(new SalesInvoiceDetail
            {
                SalesInvoiceUid = invoice.Uid,
                ProductUid = milk.Uid,
                ProductCode = milk.ProductCode,
                ProductName = milk.ProductName,
                Quantity = milkQty,
                UnitPrice = 0,
                DiscountPercent = 0,
                DiscountAmount = 0,
                TaxPercent = 0,
                TaxAmount = 0,
                LineTotal = 0,
                CostPrice = milk.PurchasePrice,
                Remarks = "Consumed for yogurt production (40 milk : 32 yogurt)"
            });

            await AdjustInventoryAsync(
                warehouseUid, milk.Uid, milk.ProductName, milkQty, yogurt.Uid, yogurt.ProductName, yogurtQty,
                invoice.Uid, invoiceNo, userUid, terminalUid, now, ct);

            await _db.SaveChangesAsync(ct);
            await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(), remarks, ct);
            await tx.CommitAsync(ct);

            TempData["Success"] =
                $"Produced {yogurtQty:0.###} kg yogurt (used {milkQty:0.###} kg milk). Sales invoice {invoiceNo} created.";
            return RedirectToAction("Details", "SalesInvoices", new { id = invoice.Uid });
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            ModelState.AddModelError(string.Empty, ex.Message);
            page = await BuildPageModelAsync(yogurtQty, ct);
            return View("Index", page);
        }
    }

    private async Task<YogurtProductionViewModel> BuildPageModelAsync(decimal yogurtQty, CancellationToken ct)
    {
        var milk = await FindMilkProductAsync(ct);
        var yogurt = await FindYogurtProductAsync(ct);
        var (branchName, branchUid) = await ResolveSessionBranchAsync(ct);

        decimal milkAvail = 0, yogurtAvail = 0;
        string? warehouseHint = null;
        if (branchUid is > 0)
        {
            try
            {
                var wh = await _inventory.ResolveWarehouseUidAsync(null, branchUid, ct);
                warehouseHint = await _db.Warehouses.AsNoTracking()
                    .Where(w => w.Uid == wh)
                    .Select(w => w.WarehouseName)
                    .FirstOrDefaultAsync(ct);

                if (milk is not null)
                {
                    milkAvail = await _db.Inventories.AsNoTracking()
                        .Where(i => i.WarehouseUid == wh && i.ProductUid == milk.Uid)
                        .Select(i => i.CurrentQuantity)
                        .FirstOrDefaultAsync(ct);
                }

                if (yogurt is not null)
                {
                    yogurtAvail = await _db.Inventories.AsNoTracking()
                        .Where(i => i.WarehouseUid == wh && i.ProductUid == yogurt.Uid)
                        .Select(i => i.CurrentQuantity)
                        .FirstOrDefaultAsync(ct);
                }
            }
            catch (InvalidOperationException)
            {
                warehouseHint = "(no warehouse)";
            }
        }

        var yQty = yogurtQty > 0 ? Math.Round(yogurtQty, 3) : 0m;
        return new YogurtProductionViewModel
        {
            YogurtQuantity = yQty,
            MilkQuantity = yQty > 0 ? MilkForYogurt(yQty) : 0m,
            MilkProductUid = milk?.Uid,
            MilkProductName = milk?.ProductName,
            MilkAvailable = milkAvail,
            YogurtProductUid = yogurt?.Uid,
            YogurtProductName = yogurt?.ProductName,
            YogurtAvailable = yogurtAvail,
            BranchName = branchName,
            WarehouseHint = warehouseHint,
            MilkPerYogurtKg = RecipeMilkKg / RecipeYogurtKg
        };
    }

    private async Task AdjustInventoryAsync(
        int warehouseUid,
        int milkUid,
        string milkName,
        decimal milkQty,
        int yogurtUid,
        string yogurtName,
        decimal yogurtQty,
        int invoiceUid,
        string invoiceNo,
        int? userUid,
        int? terminalUid,
        DateTime now,
        CancellationToken ct)
    {
        var milkInv = await GetOrCreateInventoryAsync(milkUid, warehouseUid, now, ct);
        // Always deduct full recipe qty — allow negative availability (10 − 40 = −30).
        milkInv.QuantityOut += milkQty;
        milkInv.CurrentQuantity -= milkQty;
        milkInv.LastStockDate = now;
        milkInv.UpdatedDate = now;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = now,
            ProductUid = milkUid,
            WarehouseUid = warehouseUid,
            TransactionType = TxProductionOut,
            ReferenceUid = invoiceUid,
            ReferenceNo = invoiceNo,
            QuantityIn = 0,
            QuantityOut = milkQty,
            BalanceQuantity = milkInv.CurrentQuantity,
            UnitCost = milkInv.AverageCost,
            Remarks = $"{ProductionRemarkPrefix} → {yogurtName}",
            UserUid = userUid,
            TerminalUid = terminalUid,
            CreatedDate = now
        });

        var yogurtInv = await GetOrCreateInventoryAsync(yogurtUid, warehouseUid, now, ct);
        var previousQty = yogurtInv.CurrentQuantity;
        var previousValue = previousQty * yogurtInv.AverageCost;
        var unitCost = milkQty > 0 && yogurtQty > 0
            ? Math.Round(milkInv.AverageCost * milkQty / yogurtQty, 2)
            : yogurtInv.AverageCost;

        yogurtInv.QuantityIn += yogurtQty;
        yogurtInv.CurrentQuantity += yogurtQty;
        yogurtInv.AverageCost = yogurtInv.CurrentQuantity <= 0
            ? unitCost
            : Math.Round((previousValue + yogurtQty * unitCost) / yogurtInv.CurrentQuantity, 2);
        yogurtInv.LastStockDate = now;
        yogurtInv.UpdatedDate = now;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = now,
            ProductUid = yogurtUid,
            WarehouseUid = warehouseUid,
            TransactionType = TxProductionIn,
            ReferenceUid = invoiceUid,
            ReferenceNo = invoiceNo,
            QuantityIn = yogurtQty,
            QuantityOut = 0,
            BalanceQuantity = yogurtInv.CurrentQuantity,
            UnitCost = unitCost,
            Remarks = $"{ProductionRemarkPrefix} from {milkName}",
            UserUid = userUid,
            TerminalUid = terminalUid,
            CreatedDate = now
        });
    }

    private async Task<Inventory> GetOrCreateInventoryAsync(int productUid, int warehouseUid, DateTime now, CancellationToken ct)
    {
        var inv = await _db.Inventories
            .FirstOrDefaultAsync(x => x.ProductUid == productUid && x.WarehouseUid == warehouseUid, ct);
        if (inv is not null)
            return inv;

        inv = new Inventory
        {
            ProductUid = productUid,
            WarehouseUid = warehouseUid,
            QuantityIn = 0,
            QuantityOut = 0,
            CurrentQuantity = 0,
            AverageCost = 0,
            LastStockDate = now,
            UpdatedDate = now
        };
        _db.Inventories.Add(inv);
        await _db.SaveChangesAsync(ct);
        return inv;
    }

    private async Task<Product?> FindYogurtProductAsync(CancellationToken ct)
    {
        var list = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && (x.ProductName.Contains("Yogurt") || x.ProductName.Contains("Yoghurt")))
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);
        return list.FirstOrDefault(x => x.ProductName.Contains("Fresh", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault();
    }

    private async Task<Product?> FindMilkProductAsync(CancellationToken ct)
    {
        var list = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && x.ProductName.Contains("Milk"))
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);
        return list.FirstOrDefault(x =>
                   x.ProductName.Contains("Fresh Milk 250", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault(x =>
                   x.ProductName.Contains("Fresh", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault();
    }

    private async Task<(string? TerminalName, int? TerminalUid)> ResolveSessionTerminalAsync(CancellationToken ct)
    {
        var sessionTerminal = HttpContext.Session.GetString(AccountController.SessionTerminalNameKey)
                              ?? User.FindFirstValue("TerminalName");
        if (string.IsNullOrWhiteSpace(sessionTerminal))
            return (null, null);

        var name = sessionTerminal.Trim();
        var terminalUid = await _db.Terminals.AsNoTracking()
            .Where(t => t.IsActive && t.TerminalName == name)
            .Select(t => (int?)t.Uid)
            .FirstOrDefaultAsync(ct);
        return (name, terminalUid);
    }

    private async Task<(string? BranchName, int? BranchUid)> ResolveSessionBranchAsync(CancellationToken ct)
    {
        var sessionBranch = HttpContext.Session.GetString(AccountController.SessionBranchKey)
                            ?? User.FindFirstValue("Branch");
        if (string.IsNullOrWhiteSpace(sessionBranch))
            return (null, null);

        var name = sessionBranch.Trim();
        var branchUid = await _db.Branches.AsNoTracking()
            .Where(b => b.IsActive && b.BranchName == name)
            .Select(b => (int?)b.Uid)
            .FirstOrDefaultAsync(ct);
        return (name, branchUid);
    }

    /// <summary>Sales invoice customer for yogurt production — create "Shop" if missing.</summary>
    private async Task<int> EnsureShopCustomerUidAsync(CancellationToken ct)
    {
        var existing = await _db.Customers
            .Where(x => x.CustomerName == "Shop" || x.CustomerCode == "SHOP")
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

        var shop = new Customer
        {
            CustomerCode = "SHOP",
            CustomerName = "Shop",
            IsActive = true,
            OpeningBalance = 0,
            AdvanceBalance = 0,
            CreditLimit = 0,
            DiscountPercent = 0,
            CreatedDate = DateTime.Now
        };
        _db.Customers.Add(shop);
        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "Customers", shop.Uid.ToString(), "Auto-created Shop customer for yogurt production", ct);
        return shop.Uid;
    }
}
