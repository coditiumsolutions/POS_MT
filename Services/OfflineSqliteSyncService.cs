using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Models;

namespace POS_MT.Services;

public interface IOfflineSqliteSyncService
{
    Task<string> InitAsync(CancellationToken ct = default);
    Task<string> PullAsync(CancellationToken ct = default);
    Task<string> PushAsync(CancellationToken ct = default);
    Task<string> StatusAsync(CancellationToken ct = default);
}

/// <summary>
/// CLI-backed sync between central SQL Server and local SQLite offline store.
/// </summary>
public sealed class OfflineSqliteSyncService : IOfflineSqliteSyncService
{
    private readonly IOfflineSqlitePaths _paths;
    private readonly IPosConnectionRouter _router;
    private readonly IConfiguration _configuration;
    private readonly ILogger<OfflineSqliteSyncService> _logger;

    public OfflineSqliteSyncService(
        IOfflineSqlitePaths paths,
        IPosConnectionRouter router,
        IConfiguration configuration,
        ILogger<OfflineSqliteSyncService> logger)
    {
        _paths = paths;
        _router = router;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<string> InitAsync(CancellationToken ct = default)
    {
        var cs = _paths.GetConnectionString();
        var path = _paths.ResolveFilePath();
        await using var db = CreateSqlite(cs);
        await db.Database.EnsureCreatedAsync(ct);
        return $"SQLite offline database ready: {path}";
    }

    public async Task<string> PullAsync(CancellationToken ct = default)
    {
        var onlineCs = ResolveOnlineConnectionString();

        var sqliteCs = _paths.GetConnectionString();
        await using var central = CreateSqlServer(onlineCs);
        if (!await central.Database.CanConnectAsync(ct))
        {
            throw new InvalidOperationException("Cannot connect to central/online SQL Server. Check network and ConnectionStrings:POS_MT.");
        }

        await using var local = CreateSqlite(sqliteCs);
        await local.Database.EnsureCreatedAsync(ct);

        // Catalog / lookup data used by POS. Sales + outbox on SQLite are preserved.
        var products = await central.Products.AsNoTracking().ToListAsync(ct);
        var inventories = await central.Inventories.AsNoTracking().ToListAsync(ct);
        var categories = await central.ProductCategories.AsNoTracking().ToListAsync(ct);
        var units = await central.ProductUnits.AsNoTracking().ToListAsync(ct);
        var customers = await central.Customers.AsNoTracking().ToListAsync(ct);
        var users = await central.Users.AsNoTracking().ToListAsync(ct);
        var branches = await central.Branches.AsNoTracking().ToListAsync(ct);
        var terminals = await central.Terminals.AsNoTracking().ToListAsync(ct);
        var paymentMethods = await central.PaymentMethods.AsNoTracking().ToListAsync(ct);
        var warehouses = await central.Warehouses.AsNoTracking().ToListAsync(ct);
        var vendors = await central.Vendors.AsNoTracking().ToListAsync(ct);
        var configs = await central.Configurations.AsNoTracking().ToListAsync(ct);
        var expenseCategories = await central.ExpenseCategories.AsNoTracking().ToListAsync(ct);

        await using var tx = await local.Database.BeginTransactionAsync(ct);

        await ReplaceTableAsync(local, inventories, ct);
        await ReplaceTableAsync(local, products, ct);
        await ReplaceTableAsync(local, categories, ct);
        await ReplaceTableAsync(local, units, ct);
        await ReplaceTableAsync(local, customers, ct);
        await ReplaceTableAsync(local, users, ct);
        await ReplaceTableAsync(local, branches, ct);
        await ReplaceTableAsync(local, terminals, ct);
        await ReplaceTableAsync(local, paymentMethods, ct);
        await ReplaceTableAsync(local, warehouses, ct);
        await ReplaceTableAsync(local, vendors, ct);
        await ReplaceTableAsync(local, configs, ct);
        await ReplaceTableAsync(local, expenseCategories, ct);

        await tx.CommitAsync(ct);

        var msg =
            $"Pulled to SQLite ({_paths.ResolveFilePath()}): " +
            $"{products.Count} products, {inventories.Count} inventory rows, {customers.Count} customers, {users.Count} users.";
        _logger.LogInformation("{Message}", msg);
        return msg;
    }

    public async Task<string> PushAsync(CancellationToken ct = default)
    {
        if (!_paths.FileExists())
        {
            throw new InvalidOperationException("SQLite offline file not found. Run: POS_MT.exe offline pull");
        }

        var onlineCs = ResolveOnlineConnectionString();

        var sqliteCs = _paths.GetConnectionString();
        await using var local = CreateSqlite(sqliteCs);
        await using var central = CreateSqlServer(onlineCs);

        if (!await central.Database.CanConnectAsync(ct))
        {
            throw new InvalidOperationException("Cannot connect to central/online SQL Server for push.");
        }

        var batch = await local.SyncOutboxes
            .Where(x => x.Status == SyncOutbox.StatusPending || x.Status == SyncOutbox.StatusFailed)
            .Where(x => x.AttemptCount < 10)
            .OrderBy(x => x.CreatedDate)
            .Take(100)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            return "Nothing to push — SyncOutbox has no pending/failed sales.";
        }

        var synced = 0;
        var failed = 0;
        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            item.Status = SyncOutbox.StatusSyncing;
            item.AttemptCount += 1;
            item.UpdatedDate = DateTime.Now;
            await local.SaveChangesAsync(ct);

            try
            {
                await PushSalesInvoiceAsync(local, central, item, ct);
                item.Status = SyncOutbox.StatusSynced;
                item.SyncedDate = DateTime.Now;
                item.LastError = null;
                item.UpdatedDate = DateTime.Now;
                synced++;
            }
            catch (Exception ex)
            {
                item.Status = SyncOutbox.StatusFailed;
                item.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                item.UpdatedDate = DateTime.Now;
                failed++;
                _logger.LogWarning(ex, "Push failed for outbox {Uid}", item.Uid);
            }

            await local.SaveChangesAsync(ct);
        }

        return $"Push complete: {synced} synced, {failed} failed (of {batch.Count}).";
    }

    public async Task<string> StatusAsync(CancellationToken ct = default)
    {
        var path = _paths.ResolveFilePath();
        if (!_paths.FileExists())
        {
            return $"SQLite offline file missing: {path}{Environment.NewLine}Run: dotnet run -- offline pull";
        }

        await using var local = CreateSqlite(_paths.GetConnectionString());
        var products = await local.Products.CountAsync(ct);
        var customers = await local.Customers.CountAsync(ct);
        var sales = await local.SalesInvoices.CountAsync(ct);
        var pending = await local.SyncOutboxes.CountAsync(
            x => x.Status == SyncOutbox.StatusPending || x.Status == SyncOutbox.StatusSyncing, ct);
        var failedOut = await local.SyncOutboxes.CountAsync(x => x.Status == SyncOutbox.StatusFailed, ct);
        var synced = await local.SyncOutboxes.CountAsync(x => x.Status == SyncOutbox.StatusSynced, ct);

        return
            $"SQLite: {path}{Environment.NewLine}" +
            $"Products={products}, Customers={customers}, SalesInvoices={sales}{Environment.NewLine}" +
            $"Outbox: pending={pending}, failed={failedOut}, synced={synced}";
    }

    private string ResolveOnlineConnectionString()
    {
        foreach (var candidate in new[]
                 {
                     _router.GetOnlineConnectionString(),
                     _configuration.GetConnectionString("POS_MT_Central"),
                     _configuration.GetConnectionString("POS_MT")
                 })
        {
            if (!string.IsNullOrWhiteSpace(candidate)
                && !candidate.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "Central/online SQL connection string is missing or still has placeholders. Set ConnectionStrings:POS_MT in appsettings.Development.json.");
    }

    private static async Task ReplaceTableAsync<T>(POSDbContext db, List<T> rows, CancellationToken ct) where T : class
    {
        await db.Set<T>().ExecuteDeleteAsync(ct);
        if (rows.Count > 0)
        {
            await db.Set<T>().AddRangeAsync(rows, ct);
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static POSDbContext CreateSqlite(string cs)
    {
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlite(cs)
            .Options;
        return new POSDbContext(options);
    }

    private static POSDbContext CreateSqlServer(string cs)
    {
        var options = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlServer(cs)
            .Options;
        return new POSDbContext(options);
    }

    private static async Task PushSalesInvoiceAsync(
        POSDbContext localDb,
        POSDbContext centralDb,
        SyncOutbox item,
        CancellationToken ct)
    {
        var localInvoice = await localDb.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == item.EntityUid, ct)
            ?? throw new InvalidOperationException($"Local sales invoice {item.EntityUid} not found.");

        var details = await localDb.SalesInvoiceDetails.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == localInvoice.Uid)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        await using var tx = await centralDb.Database.BeginTransactionAsync(ct);

        var centralInvoice = await centralDb.SalesInvoices
            .FirstOrDefaultAsync(x => x.ClientTransactionId == item.ClientTransactionId, ct);

        if (centralInvoice is null)
        {
            var invoiceNoClash = await centralDb.SalesInvoices.AsNoTracking()
                .AnyAsync(x => x.InvoiceNo == localInvoice.InvoiceNo, ct);
            if (invoiceNoClash)
            {
                throw new InvalidOperationException(
                    $"Central already has InvoiceNo '{localInvoice.InvoiceNo}' with a different ClientTransactionId. Not overwriting.");
            }

            centralInvoice = new SalesInvoice
            {
                ClientTransactionId = item.ClientTransactionId,
                InvoiceNo = localInvoice.InvoiceNo,
                InvoiceDate = localInvoice.InvoiceDate,
                BranchUid = localInvoice.BranchUid,
                CustomerUid = localInvoice.CustomerUid,
                UserUid = localInvoice.UserUid,
                TerminalUid = localInvoice.TerminalUid,
                PaymentMethodUid = localInvoice.PaymentMethodUid,
                SubTotal = localInvoice.SubTotal,
                DiscountAmount = localInvoice.DiscountAmount,
                TaxAmount = localInvoice.TaxAmount,
                OtherCharges = localInvoice.OtherCharges,
                NetAmount = localInvoice.NetAmount,
                PaidAmount = localInvoice.PaidAmount,
                BalanceAmount = localInvoice.BalanceAmount,
                PaymentStatus = localInvoice.PaymentStatus,
                InvoiceStatus = localInvoice.InvoiceStatus,
                Remarks = localInvoice.Remarks,
                CreatedDate = localInvoice.CreatedDate,
                UpdatedDate = localInvoice.UpdatedDate
            };
            centralDb.SalesInvoices.Add(centralInvoice);
            await centralDb.SaveChangesAsync(ct);
        }
        else
        {
            centralInvoice.SubTotal = localInvoice.SubTotal;
            centralInvoice.DiscountAmount = localInvoice.DiscountAmount;
            centralInvoice.TaxAmount = localInvoice.TaxAmount;
            centralInvoice.OtherCharges = localInvoice.OtherCharges;
            centralInvoice.NetAmount = localInvoice.NetAmount;
            centralInvoice.PaidAmount = localInvoice.PaidAmount;
            centralInvoice.BalanceAmount = localInvoice.BalanceAmount;
            centralInvoice.PaymentStatus = localInvoice.PaymentStatus;
            centralInvoice.InvoiceStatus = localInvoice.InvoiceStatus;
            centralInvoice.Remarks = localInvoice.Remarks;
            centralInvoice.UpdatedDate = DateTime.Now;

            var oldDetails = await centralDb.SalesInvoiceDetails
                .Where(x => x.SalesInvoiceUid == centralInvoice.Uid)
                .ToListAsync(ct);
            if (oldDetails.Count > 0)
            {
                centralDb.SalesInvoiceDetails.RemoveRange(oldDetails);
            }
        }

        foreach (var d in details)
        {
            centralDb.SalesInvoiceDetails.Add(new SalesInvoiceDetail
            {
                SalesInvoiceUid = centralInvoice.Uid,
                ProductUid = d.ProductUid,
                ProductCode = d.ProductCode,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountPercent = d.DiscountPercent,
                DiscountAmount = d.DiscountAmount,
                TaxPercent = d.TaxPercent,
                TaxAmount = d.TaxAmount,
                LineTotal = d.LineTotal,
                CostPrice = d.CostPrice,
                Remarks = d.Remarks
            });
        }

        await centralDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
