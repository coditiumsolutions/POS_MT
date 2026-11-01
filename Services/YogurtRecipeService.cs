using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class YogurtRecipeService : IYogurtRecipeService
{
    public const decimal DefaultRecipeMilkKg = 40m;
    public const decimal DefaultRecipeYogurtKg = 32m;
    private const string TxProductionOut = "ProductionOut";
    private const string ProductionRemarkPrefix = "Yogurt production";

    private readonly POSDbContext _db;
    private readonly IAuditService _audit;

    public YogurtRecipeService(POSDbContext db, IAuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public decimal RecipeMilkKg => DefaultRecipeMilkKg;
    public decimal RecipeYogurtKg => DefaultRecipeYogurtKg;

    public decimal MilkForYogurt(decimal yogurtKg)
        => Math.Round(yogurtKg * DefaultRecipeMilkKg / DefaultRecipeYogurtKg, 3);

    public async Task<YogurtRecipeApplyResult> ConsumeMilkForReceivedYogurtAsync(
        decimal yogurtQty,
        int warehouseUid,
        int? branchUid,
        string? branchName,
        int? userUid,
        int? terminalUid,
        string? terminalName,
        string sourceReferenceNo,
        CancellationToken ct = default)
    {
        yogurtQty = Math.Round(yogurtQty, 3);
        if (yogurtQty <= 0 || warehouseUid <= 0)
            return new YogurtRecipeApplyResult { Applied = false, Message = "No yogurt quantity." };

        var yogurt = await FindYogurtProductAsync(ct);
        var milk = await FindMilkProductAsync(ct);
        if (yogurt is null || milk is null)
        {
            return new YogurtRecipeApplyResult
            {
                Applied = false,
                Message = "Products 'Fresh Milk 250' / 'Fresh Yogurt' were not found."
            };
        }

        var milkQty = MilkForYogurt(yogurtQty);
        var now = DateTime.Now;
        var shopCustomerUid = await EnsureShopCustomerUidAsync(ct);

        var paymentMethodUid = await _db.PaymentMethods.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderByDescending(x => x.PaymentName.Contains("Cash"))
            .ThenBy(x => x.PaymentName)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(ct);

        var yogurtLineTotal = Math.Round(yogurtQty * yogurt.SalePrice, 2);
        var invoiceNo = $"YOG-{now:yyyyMMddHHmmssfff}";
        while (await _db.SalesInvoices.AnyAsync(x => x.InvoiceNo == invoiceNo, ct))
            invoiceNo = $"YOG-{now:yyyyMMddHHmmssfff}-{Random.Shared.Next(10, 99)}";

        var remarks =
            $"{ProductionRemarkPrefix}: {milkQty:0.###} kg {milk.ProductName} → {yogurtQty:0.###} kg {yogurt.ProductName} (40:32) via {sourceReferenceNo}";

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
            Remarks = $"Produced from {milkQty:0.###} kg milk ({sourceReferenceNo})"
        });

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

        var milkInv = await GetOrCreateInventoryAsync(milk.Uid, warehouseUid, now, ct);
        milkInv.QuantityOut += milkQty;
        milkInv.CurrentQuantity -= milkQty;
        milkInv.LastStockDate = now;
        milkInv.UpdatedDate = now;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = now,
            ProductUid = milk.Uid,
            WarehouseUid = warehouseUid,
            TransactionType = TxProductionOut,
            ReferenceUid = invoice.Uid,
            ReferenceNo = invoiceNo,
            QuantityIn = 0,
            QuantityOut = milkQty,
            BalanceQuantity = milkInv.CurrentQuantity,
            UnitCost = milkInv.AverageCost,
            Remarks = $"{ProductionRemarkPrefix} → {yogurt.ProductName} ({sourceReferenceNo})",
            UserUid = userUid,
            TerminalUid = terminalUid,
            CreatedDate = now
        });

        await _db.SaveChangesAsync(ct);
        await _audit.WriteAsync("Create", "SalesInvoices", invoice.Uid.ToString(), remarks, ct);

        return new YogurtRecipeApplyResult
        {
            Applied = true,
            YogurtQuantity = yogurtQty,
            MilkQuantity = milkQty,
            MilkProductName = milk.ProductName,
            MilkBalanceAfter = milkInv.CurrentQuantity,
            SalesInvoiceUid = invoice.Uid,
            SalesInvoiceNo = invoiceNo,
            Message =
                $"Used {milkQty:0.###} kg {milk.ProductName} (balance {milkInv.CurrentQuantity:0.###}). Sales invoice {invoiceNo} (Shop)."
        };
    }

    public async Task<Product?> FindMilkProductAsync(CancellationToken ct)
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

    public async Task<Product?> FindYogurtProductAsync(CancellationToken ct)
    {
        var list = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive && (x.ProductName.Contains("Yogurt") || x.ProductName.Contains("Yoghurt")))
            .OrderBy(x => x.ProductName)
            .ToListAsync(ct);

        return list.FirstOrDefault(x => x.ProductName.Contains("Fresh", StringComparison.OrdinalIgnoreCase))
               ?? list.FirstOrDefault();
    }

    public static bool IsFreshYogurtProduct(string? productName)
    {
        if (string.IsNullOrWhiteSpace(productName))
            return false;
        var n = productName.Trim();
        return n.Contains("Yogurt", StringComparison.OrdinalIgnoreCase)
               || n.Contains("Yoghurt", StringComparison.OrdinalIgnoreCase);
    }

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
        await _audit.WriteAsync("Create", "Customers", shop.Uid.ToString(),
            "Auto-created Shop customer for yogurt production", ct);
        return shop.Uid;
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
}
