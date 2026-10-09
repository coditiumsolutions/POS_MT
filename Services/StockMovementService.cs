using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Options;
using POS_MT.ViewModels;

namespace POS_MT.Services;

public sealed class StockMovementService : IStockMovementService
{
    private readonly POSDbContext _db;
    private readonly StockMovementOptions _options;

    public StockMovementService(POSDbContext db, IOptions<StockMovementOptions> options)
    {
        _db = db;
        _options = options.Value;
    }

    public async Task<StockMovementIndexViewModel> AnalyzeAsync(string? classificationFilter = null, CancellationToken cancellationToken = default)
    {
        var today = DateTime.Today;
        var trendDays = Math.Max(7, _options.TrendWindowDays);
        var recentStart = today.AddDays(-trendDays);
        var priorStart = today.AddDays(-trendDays * 2);
        var tomorrow = today.AddDays(1);

        var products = await _db.Products.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => new
            {
                x.Uid,
                x.ProductCode,
                x.ProductName,
                x.PurchasePrice,
                x.MaximumStock
            })
            .ToListAsync(cancellationToken);

        var inventoryByProduct = await _db.Inventories.AsNoTracking()
            .GroupBy(x => x.ProductUid)
            .Select(g => new
            {
                ProductUid = g.Key,
                CurrentStock = g.Sum(x => x.CurrentQuantity),
                MaxStock = g.Max(x => x.MaximumStock),
                UnitCost = g.Average(x => x.AverageCost > 0 ? x.AverageCost : x.LastPurchasePrice)
            })
            .ToListAsync(cancellationToken);

        var inventoryLookup = inventoryByProduct.ToDictionary(x => x.ProductUid);

        var lastSales = await (
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            where inv.InvoiceStatus != "Cancelled"
            group inv by d.ProductUid into g
            select new
            {
                ProductUid = g.Key,
                LastSaleDate = (DateTime?)g.Max(x => x.InvoiceDate)
            }
        ).ToListAsync(cancellationToken);

        var lastSaleLookup = lastSales.ToDictionary(x => x.ProductUid, x => x.LastSaleDate);

        var recentQty = await (
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            where inv.InvoiceStatus != "Cancelled"
                  && inv.InvoiceDate >= recentStart
                  && inv.InvoiceDate < tomorrow
            group d by d.ProductUid into g
            select new { ProductUid = g.Key, Qty = g.Sum(x => x.Quantity) }
        ).ToListAsync(cancellationToken);

        var recentLookup = recentQty.ToDictionary(x => x.ProductUid, x => x.Qty);

        var priorQty = await (
            from d in _db.SalesInvoiceDetails.AsNoTracking()
            join inv in _db.SalesInvoices.AsNoTracking() on d.SalesInvoiceUid equals inv.Uid
            where inv.InvoiceStatus != "Cancelled"
                  && inv.InvoiceDate >= priorStart
                  && inv.InvoiceDate < recentStart
            group d by d.ProductUid into g
            select new { ProductUid = g.Key, Qty = g.Sum(x => x.Quantity) }
        ).ToListAsync(cancellationToken);

        var priorLookup = priorQty.ToDictionary(x => x.ProductUid, x => x.Qty);

        var lastPurchases = await (
            from d in _db.PurchaseInvoiceDetails.AsNoTracking()
            join inv in _db.PurchaseInvoices.AsNoTracking() on d.PurchaseInvoiceUid equals inv.Uid
            where inv.InvoiceStatus != "Cancelled"
            group inv by d.ProductUid into g
            select new
            {
                ProductUid = g.Key,
                LastPurchaseDate = (DateTime?)g.Max(x => x.PurchaseDate)
            }
        ).ToListAsync(cancellationToken);

        var lastPurchaseLookup = lastPurchases.ToDictionary(x => x.ProductUid, x => x.LastPurchaseDate);

        var rows = new List<StockMovementRowViewModel>();
        foreach (var product in products)
        {
            inventoryLookup.TryGetValue(product.Uid, out var inv);
            var currentStock = inv?.CurrentStock ?? 0m;
            if (currentStock <= _options.MinimumStockToInclude)
            {
                continue;
            }

            lastSaleLookup.TryGetValue(product.Uid, out var lastSale);
            recentLookup.TryGetValue(product.Uid, out var recentSalesQty);
            priorLookup.TryGetValue(product.Uid, out var priorSalesQty);
            lastPurchaseLookup.TryGetValue(product.Uid, out var lastPurchase);

            var unitCost = inv is not null && inv.UnitCost > 0
                ? inv.UnitCost
                : product.PurchasePrice;
            var daysSinceSale = lastSale.HasValue
                ? (int?)(today - lastSale.Value.Date).TotalDays
                : null;

            var trend = ResolveTrend(recentSalesQty, priorSalesQty);
            var classification = Classify(daysSinceSale, trend);
            var maxStock = inv is not null && inv.MaxStock > 0 ? inv.MaxStock : product.MaximumStock;
            var isExcess = maxStock > 0 && currentStock > maxStock;

            rows.Add(new StockMovementRowViewModel
            {
                ProductUid = product.Uid,
                ProductCode = product.ProductCode,
                ProductName = product.ProductName,
                CurrentStock = currentStock,
                EstimatedStockValue = Math.Round(currentStock * unitCost, 2),
                LastSaleDate = lastSale,
                DaysSinceLastSale = daysSinceSale,
                LastPurchaseDate = lastPurchase,
                RecentSalesQty = recentSalesQty,
                PriorSalesQty = priorSalesQty,
                SalesTrend = trend,
                Classification = classification,
                IsExcessInventory = isExcess
            });
        }

        rows = rows
            .OrderByDescending(x => x.Classification == StockMovementClasses.DeadStock)
            .ThenByDescending(x => x.Classification == StockMovementClasses.SlowMoving)
            .ThenByDescending(x => x.DaysSinceLastSale ?? int.MaxValue)
            .ThenBy(x => x.ProductName)
            .ToList();

        var filter = NormalizeClassification(classificationFilter);
        var filtered = string.IsNullOrEmpty(filter)
            ? rows
            : rows.Where(x => string.Equals(x.Classification, filter, StringComparison.OrdinalIgnoreCase)).ToList();

        return new StockMovementIndexViewModel
        {
            Classification = filter,
            ClassificationOptions =
            [
                new SelectListItem { Value = "", Text = "All", Selected = string.IsNullOrEmpty(filter) },
                new SelectListItem { Value = StockMovementClasses.FastMoving, Text = StockMovementClasses.FastMoving, Selected = filter == StockMovementClasses.FastMoving },
                new SelectListItem { Value = StockMovementClasses.SlowMoving, Text = StockMovementClasses.SlowMoving, Selected = filter == StockMovementClasses.SlowMoving },
                new SelectListItem { Value = StockMovementClasses.DeadStock, Text = StockMovementClasses.DeadStock, Selected = filter == StockMovementClasses.DeadStock }
            ],
            FastMovingMaxDaysSinceSale = _options.FastMovingMaxDaysSinceSale,
            SlowMovingMinDaysSinceSale = _options.SlowMovingMinDaysSinceSale,
            DeadStockMinDaysSinceSale = _options.DeadStockMinDaysSinceSale,
            TrendWindowDays = trendDays,
            FastMovingCount = rows.Count(x => x.Classification == StockMovementClasses.FastMoving),
            SlowMovingCount = rows.Count(x => x.Classification == StockMovementClasses.SlowMoving),
            DeadStockCount = rows.Count(x => x.Classification == StockMovementClasses.DeadStock),
            TotalStockValue = filtered.Sum(x => x.EstimatedStockValue),
            Rows = filtered
        };
    }

    private string Classify(int? daysSinceLastSale, string trend)
    {
        if (!daysSinceLastSale.HasValue || daysSinceLastSale.Value >= _options.DeadStockMinDaysSinceSale)
        {
            return StockMovementClasses.DeadStock;
        }

        if (daysSinceLastSale.Value >= _options.SlowMovingMinDaysSinceSale
            || string.Equals(trend, "Declining", StringComparison.OrdinalIgnoreCase))
        {
            return StockMovementClasses.SlowMoving;
        }

        if (daysSinceLastSale.Value <= _options.FastMovingMaxDaysSinceSale)
        {
            return StockMovementClasses.FastMoving;
        }

        return StockMovementClasses.SlowMoving;
    }

    private string ResolveTrend(decimal recentQty, decimal priorQty)
    {
        if (recentQty <= 0 && priorQty <= 0)
        {
            return "No sales";
        }

        if (priorQty <= 0 && recentQty > 0)
        {
            return "Rising";
        }

        if (recentQty <= 0 && priorQty > 0)
        {
            return "Declining";
        }

        var ratio = recentQty / priorQty;
        if (ratio < _options.DecliningTrendRatio)
        {
            return "Declining";
        }

        if (ratio > (2m - _options.DecliningTrendRatio))
        {
            return "Rising";
        }

        return "Stable";
    }

    private static string? NormalizeClassification(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, StockMovementClasses.FastMoving, StringComparison.OrdinalIgnoreCase))
        {
            return StockMovementClasses.FastMoving;
        }

        if (string.Equals(value, StockMovementClasses.SlowMoving, StringComparison.OrdinalIgnoreCase))
        {
            return StockMovementClasses.SlowMoving;
        }

        if (string.Equals(value, StockMovementClasses.DeadStock, StringComparison.OrdinalIgnoreCase))
        {
            return StockMovementClasses.DeadStock;
        }

        return null;
    }
}
