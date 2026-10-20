using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class InventoryLedgerService : IInventoryLedgerService
{
    public const string TxSale = "Sale";
    public const string TxSaleReversal = "SaleReversal";
    public const string TxPurchase = "Purchase";
    public const string TxPurchaseReversal = "PurchaseReversal";
    public const string StockShortMarker = "Stock short:";

    private readonly POSDbContext _db;

    public InventoryLedgerService(POSDbContext db)
    {
        _db = db;
    }

    public async Task<int> ResolveWarehouseUidAsync(int? preferredWarehouseUid, int? branchUid, CancellationToken cancellationToken = default)
    {
        if (preferredWarehouseUid is > 0)
        {
            var preferred = await _db.Warehouses.AsNoTracking()
                .Where(x => x.Uid == preferredWarehouseUid && x.IsActive)
                .Select(x => (int?)x.Uid)
                .FirstOrDefaultAsync(cancellationToken);
            if (preferred is > 0)
                return preferred.Value;
        }

        if (branchUid is > 0)
        {
            var byBranch = await _db.Warehouses.AsNoTracking()
                .Where(x => x.IsActive && x.BranchUid == branchUid)
                .OrderBy(x => x.Uid)
                .Select(x => (int?)x.Uid)
                .FirstOrDefaultAsync(cancellationToken);
            if (byBranch is > 0)
                return byBranch.Value;
        }

        var any = await _db.Warehouses.AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Uid)
            .Select(x => (int?)x.Uid)
            .FirstOrDefaultAsync(cancellationToken);

        if (any is > 0)
            return any.Value;

        throw new InvalidOperationException("No active warehouse found. Create a warehouse before selling or purchasing stock.");
    }

    public async Task<InventoryApplyResult> ApplySaleAsync(
        int salesInvoiceUid,
        string invoiceNo,
        IReadOnlyList<InventoryLineMovement> lines,
        int? warehouseUid,
        int? branchUid,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default)
    {
        var result = new InventoryApplyResult();
        if (lines.Count == 0)
            return result;

        var alreadyApplied = await _db.InventoryTransactions.AsNoTracking()
            .AnyAsync(x => x.ReferenceUid == salesInvoiceUid && x.TransactionType == TxSale, cancellationToken);
        if (alreadyApplied)
            return result;

        var wh = await ResolveWarehouseUidAsync(warehouseUid, branchUid, cancellationToken);
        var now = DateTime.Now;

        foreach (var group in lines.Where(x => x.ProductUid > 0 && x.Quantity > 0)
                     .GroupBy(x => x.ProductUid))
        {
            var qty = group.Sum(x => x.Quantity);
            var name = group.Select(x => x.ProductName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                       ?? group.Key.ToString();
            var unitCost = group.Last().UnitCost;
            await DeductStockAsync(wh, group.Key, name, qty, unitCost, salesInvoiceUid, invoiceNo,
                userUid, terminalUid, now, result, cancellationToken);
        }

        return result;
    }

    public async Task<InventoryApplyResult> ApplySaleQuantityDeltaAsync(
        int salesInvoiceUid,
        string invoiceNo,
        InventoryLineMovement lineDelta,
        int? warehouseUid,
        int? branchUid,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default)
    {
        var result = new InventoryApplyResult();
        if (lineDelta.ProductUid <= 0 || lineDelta.Quantity == 0)
            return result;

        var wh = await ResolveWarehouseUidAsync(warehouseUid, branchUid, cancellationToken);
        var now = DateTime.Now;
        var name = string.IsNullOrWhiteSpace(lineDelta.ProductName)
            ? lineDelta.ProductUid.ToString()
            : lineDelta.ProductName.Trim();

        if (lineDelta.Quantity > 0)
        {
            await DeductStockAsync(wh, lineDelta.ProductUid, name, lineDelta.Quantity, lineDelta.UnitCost,
                salesInvoiceUid, invoiceNo, userUid, terminalUid, now, result, cancellationToken);
        }
        else
        {
            await RestoreStockAsync(wh, lineDelta.ProductUid, name, Math.Abs(lineDelta.Quantity), lineDelta.UnitCost,
                salesInvoiceUid, invoiceNo, userUid, terminalUid, now, TxSaleReversal,
                $"Qty reduce on {invoiceNo}", cancellationToken);
        }

        return result;
    }

    public async Task ReverseSaleAsync(
        int salesInvoiceUid,
        string invoiceNo,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default)
    {
        var sales = await _db.InventoryTransactions
            .Where(x => x.ReferenceUid == salesInvoiceUid
                        && (x.TransactionType == TxSale || x.TransactionType == TxSaleReversal))
            .OrderBy(x => x.Uid)
            .ToListAsync(cancellationToken);

        if (sales.Count == 0)
            return;

        var alreadyReversed = sales.Any(x => x.TransactionType == TxSaleReversal
                                             && x.Remarks != null
                                             && x.Remarks.StartsWith("Cancel ", StringComparison.OrdinalIgnoreCase));
        if (alreadyReversed)
            return;

        var netByProduct = sales
            .GroupBy(x => new { x.ProductUid, x.WarehouseUid })
            .Select(g => new
            {
                g.Key.ProductUid,
                g.Key.WarehouseUid,
                QtyOut = g.Sum(x => x.QuantityOut) - g.Sum(x => x.QuantityIn),
                UnitCost = g.OrderByDescending(x => x.Uid).Select(x => x.UnitCost).FirstOrDefault()
            })
            .Where(x => x.QtyOut > 0)
            .ToList();

        var now = DateTime.Now;
        foreach (var row in netByProduct)
        {
            var inv = await GetOrCreateInventoryAsync(row.ProductUid, row.WarehouseUid, now, cancellationToken);
            inv.QuantityIn += row.QtyOut;
            inv.CurrentQuantity += row.QtyOut;
            inv.LastStockDate = now;
            inv.UpdatedDate = now;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = now,
                ProductUid = row.ProductUid,
                WarehouseUid = row.WarehouseUid,
                TransactionType = TxSaleReversal,
                ReferenceUid = salesInvoiceUid,
                ReferenceNo = invoiceNo,
                QuantityIn = row.QtyOut,
                QuantityOut = 0,
                BalanceQuantity = inv.CurrentQuantity,
                UnitCost = row.UnitCost,
                Remarks = $"Cancel {invoiceNo}",
                UserUid = userUid,
                TerminalUid = terminalUid,
                CreatedDate = now
            });
        }
    }

    public async Task ApplyPurchaseAsync(
        int purchaseInvoiceUid,
        string purchaseInvoiceNo,
        int warehouseUid,
        IReadOnlyList<InventoryLineMovement> lines,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default,
        bool allowAdditional = false)
    {
        if (lines.Count == 0 || warehouseUid <= 0)
            return;

        if (!allowAdditional)
        {
            var alreadyApplied = await _db.InventoryTransactions.AsNoTracking()
                .AnyAsync(x => x.ReferenceUid == purchaseInvoiceUid && x.TransactionType == TxPurchase, cancellationToken);
            if (alreadyApplied)
                return;
        }

        var now = DateTime.Now;
        foreach (var group in lines.Where(x => x.ProductUid > 0 && x.Quantity > 0)
                     .GroupBy(x => x.ProductUid))
        {
            var qty = group.Sum(x => x.Quantity);
            var unitCost = group.Last().UnitCost;
            var name = group.Select(x => x.ProductName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))
                       ?? group.Key.ToString();

            var inv = await GetOrCreateInventoryAsync(group.Key, warehouseUid, now, cancellationToken);
            var previousQty = inv.CurrentQuantity;
            var previousValue = previousQty * inv.AverageCost;

            inv.QuantityIn += qty;
            inv.CurrentQuantity += qty;
            inv.LastPurchasePrice = unitCost;
            inv.AverageCost = inv.CurrentQuantity <= 0
                ? unitCost
                : Math.Round((previousValue + qty * unitCost) / inv.CurrentQuantity, 2);
            inv.LastStockDate = now;
            inv.UpdatedDate = now;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = now,
                ProductUid = group.Key,
                WarehouseUid = warehouseUid,
                TransactionType = TxPurchase,
                ReferenceUid = purchaseInvoiceUid,
                ReferenceNo = purchaseInvoiceNo,
                QuantityIn = qty,
                QuantityOut = 0,
                BalanceQuantity = inv.CurrentQuantity,
                UnitCost = unitCost,
                Remarks = $"Purchase {purchaseInvoiceNo} ({name})",
                UserUid = userUid,
                TerminalUid = terminalUid,
                CreatedDate = now
            });
        }
    }

    public async Task ReversePurchaseAsync(
        int purchaseInvoiceUid,
        string purchaseInvoiceNo,
        int? userUid,
        int? terminalUid,
        CancellationToken cancellationToken = default)
    {
        var purchases = await _db.InventoryTransactions
            .Where(x => x.ReferenceUid == purchaseInvoiceUid && x.TransactionType == TxPurchase)
            .ToListAsync(cancellationToken);

        if (purchases.Count == 0)
            return;

        var reversalRemarks = await _db.InventoryTransactions.AsNoTracking()
            .Where(x => x.ReferenceUid == purchaseInvoiceUid && x.TransactionType == TxPurchaseReversal)
            .Select(x => x.Remarks)
            .ToListAsync(cancellationToken);
        var alreadyReversed = reversalRemarks.Any(remarks =>
            remarks != null && remarks.StartsWith("Cancel ", StringComparison.OrdinalIgnoreCase));
        if (alreadyReversed)
            return;

        var now = DateTime.Now;
        foreach (var row in purchases)
        {
            var inv = await GetOrCreateInventoryAsync(row.ProductUid, row.WarehouseUid, now, cancellationToken);
            var qty = row.QuantityIn;
            var deduct = Math.Min(qty, Math.Max(0, inv.CurrentQuantity));
            inv.QuantityOut += deduct;
            inv.CurrentQuantity -= deduct;
            inv.LastStockDate = now;
            inv.UpdatedDate = now;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = now,
                ProductUid = row.ProductUid,
                WarehouseUid = row.WarehouseUid,
                TransactionType = TxPurchaseReversal,
                ReferenceUid = purchaseInvoiceUid,
                ReferenceNo = purchaseInvoiceNo,
                QuantityIn = 0,
                QuantityOut = deduct,
                BalanceQuantity = inv.CurrentQuantity,
                UnitCost = row.UnitCost,
                Remarks = $"Cancel {purchaseInvoiceNo}",
                UserUid = userUid,
                TerminalUid = terminalUid,
                CreatedDate = now
            });
        }
    }

    public string MergeShortageRemarks(string? existingRemarks, InventoryApplyResult result)
    {
        if (!result.HasShortages || result.RemarksSuffix is null)
            return existingRemarks ?? string.Empty;

        var existing = existingRemarks ?? string.Empty;
        var toAdd = result.ShortageNotes
            .Where(n => !string.IsNullOrWhiteSpace(n) && !existing.Contains(n, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (toAdd.Count == 0)
            return Truncate(existing, 500);

        string merged;
        if (existing.Contains(StockShortMarker, StringComparison.OrdinalIgnoreCase))
            merged = $"{existing.Trim()} ; {string.Join("; ", toAdd)}";
        else if (string.IsNullOrWhiteSpace(existing))
            merged = result.RemarksSuffix;
        else
            merged = $"{existing.Trim()} | {StockShortMarker} {string.Join("; ", toAdd)}";

        return Truncate(merged, 500);
    }

    private async Task DeductStockAsync(
        int warehouseUid,
        int productUid,
        string productName,
        decimal requestedQty,
        decimal unitCost,
        int referenceUid,
        string referenceNo,
        int? userUid,
        int? terminalUid,
        DateTime now,
        InventoryApplyResult result,
        CancellationToken cancellationToken)
    {
        var inv = await GetOrCreateInventoryAsync(productUid, warehouseUid, now, cancellationToken);
        var available = Math.Max(0, inv.CurrentQuantity);
        var deduct = Math.Min(requestedQty, available);
        var shortfall = requestedQty - deduct;

        if (deduct > 0)
        {
            inv.QuantityOut += deduct;
            inv.CurrentQuantity -= deduct;
            inv.LastSalePrice = unitCost;
            inv.LastStockDate = now;
            inv.UpdatedDate = now;

            _db.InventoryTransactions.Add(new InventoryTransaction
            {
                TransactionDate = now,
                ProductUid = productUid,
                WarehouseUid = warehouseUid,
                TransactionType = TxSale,
                ReferenceUid = referenceUid,
                ReferenceNo = referenceNo,
                QuantityIn = 0,
                QuantityOut = deduct,
                BalanceQuantity = inv.CurrentQuantity,
                UnitCost = unitCost,
                Remarks = $"Sale {referenceNo}",
                UserUid = userUid,
                TerminalUid = terminalUid,
                CreatedDate = now
            });
        }

        if (shortfall > 0)
        {
            result.ShortageNotes.Add(
                $"{productName} short {shortfall:0.###} (requested {requestedQty:0.###}, available {available:0.###})");
        }
    }

    private async Task RestoreStockAsync(
        int warehouseUid,
        int productUid,
        string productName,
        decimal qty,
        decimal unitCost,
        int referenceUid,
        string referenceNo,
        int? userUid,
        int? terminalUid,
        DateTime now,
        string transactionType,
        string remarks,
        CancellationToken cancellationToken)
    {
        if (qty <= 0)
            return;

        var inv = await GetOrCreateInventoryAsync(productUid, warehouseUid, now, cancellationToken);
        inv.QuantityIn += qty;
        inv.CurrentQuantity += qty;
        inv.LastStockDate = now;
        inv.UpdatedDate = now;

        _db.InventoryTransactions.Add(new InventoryTransaction
        {
            TransactionDate = now,
            ProductUid = productUid,
            WarehouseUid = warehouseUid,
            TransactionType = transactionType,
            ReferenceUid = referenceUid,
            ReferenceNo = referenceNo,
            QuantityIn = qty,
            QuantityOut = 0,
            BalanceQuantity = inv.CurrentQuantity,
            UnitCost = unitCost,
            Remarks = $"{remarks} ({productName})",
            UserUid = userUid,
            TerminalUid = terminalUid,
            CreatedDate = now
        });
    }

    private async Task<Inventory> GetOrCreateInventoryAsync(
        int productUid,
        int warehouseUid,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var inv = await _db.Inventories
            .FirstOrDefaultAsync(x => x.ProductUid == productUid && x.WarehouseUid == warehouseUid, cancellationToken);

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
            LastPurchasePrice = 0,
            LastSalePrice = 0,
            MinimumStock = 0,
            MaximumStock = 0,
            LastStockDate = now,
            UpdatedDate = now
        };
        _db.Inventories.Add(inv);
        await _db.SaveChangesAsync(cancellationToken);
        return inv;
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max)
            return value;
        return value[..(max - 3)] + "...";
    }
}
