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
        await EnsureLocalSqliteColumnsAsync(local, ct);
        await OfflineSyncCatalogService.EnsureOnAsync(central, ct);
        await OfflineSyncCatalogService.EnsureOnAsync(local, ct);

        var pullNames = await OfflineSyncCatalogService.GetPullNamesOnAsync(central, ct);
        if (pullNames.Count == 0)
        {
            return "No tables marked PullFromOnline in OfflineSyncTables. Open Setup → Sync Tables to enable pull.";
        }

        // Catalog / lookup data used by POS. Sales + outbox on SQLite are preserved unless pulled.
        var pulled = new List<string>();
        await using var tx = await local.Database.BeginTransactionAsync(ct);

        async Task PullIfEnabledAsync<T>(string tableName, IQueryable<T> query) where T : class
        {
            if (!pullNames.Contains(tableName))
            {
                return;
            }

            try
            {
                var rows = await query.AsNoTracking().ToListAsync(ct);
                await ReplaceTableAsync(local, rows, ct);
                pulled.Add($"{tableName}={rows.Count}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Pull failed on table '{tableName}': {FlattenDbError(ex)}", ex);
            }
        }

        // Lookups first (parents before dependents where possible).
        await PullIfEnabledAsync("OfflineSyncTables", central.OfflineSyncTables);
        await PullIfEnabledAsync("Branches", central.Branches);
        await PullIfEnabledAsync("Warehouses", central.Warehouses);
        await PullIfEnabledAsync("Terminals", central.Terminals);
        await PullIfEnabledAsync("PaymentMethods", central.PaymentMethods);
        await PullIfEnabledAsync("ProductCategories", central.ProductCategories);
        await PullIfEnabledAsync("ProductUnits", central.ProductUnits);
        await PullIfEnabledAsync("Products", central.Products);
        await PullIfEnabledAsync("Inventories", central.Inventories);
        await PullIfEnabledAsync("Customers", central.Customers);
        await PullIfEnabledAsync("Users", central.Users);
        await PullIfEnabledAsync("Vendors", central.Vendors);
        await PullIfEnabledAsync("Configurations", central.Configurations);
        await PullIfEnabledAsync("ExpenseCategories", central.ExpenseCategories);
        await PullIfEnabledAsync("CustomerMonthlySupplies", central.CustomerMonthlySupplies);
        await PullIfEnabledAsync("CustomerMonthlySupplyOverrides", central.CustomerMonthlySupplyOverrides);

        // Sales: only when flagged. Skipped if local SyncOutbox still has pending/failed sales
        // so unsynced offline invoices are not wiped by a full replace.
        if (pullNames.Contains("SalesInvoices") || pullNames.Contains("SalesInvoiceDetails"))
        {
            var hasPendingSales = await local.SyncOutboxes.AsNoTracking().AnyAsync(
                x => (x.Status == SyncOutbox.StatusPending || x.Status == SyncOutbox.StatusFailed)
                     && x.EntityType == "SalesInvoice",
                ct);
            if (hasPendingSales)
            {
                pulled.Add("SalesInvoices=skipped(pending outbox)");
            }
            else
            {
                try
                {
                    await local.SalesInvoiceDetails.ExecuteDeleteAsync(ct);
                    await local.SalesInvoices.ExecuteDeleteAsync(ct);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Pull failed clearing local sales: {FlattenDbError(ex)}", ex);
                }

                // Parent invoices are required when details are pulled.
                var pullSales = pullNames.Contains("SalesInvoices") || pullNames.Contains("SalesInvoiceDetails");
                if (pullSales)
                {
                    pullNames.Add("SalesInvoices");
                }

                await PullIfEnabledAsync("SalesInvoices", central.SalesInvoices);
                await PullIfEnabledAsync("SalesInvoiceDetails", central.SalesInvoiceDetails);
            }
        }

        await tx.CommitAsync(ct);

        var msg =
            $"Pulled to SQLite ({_paths.ResolveFilePath()}): " +
            (pulled.Count == 0 ? "nothing matched the pull list." : string.Join(", ", pulled));
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
        db.ChangeTracker.Clear();
        if (rows.Count == 0)
        {
            return;
        }

        // Keep explicit primary keys from online so FKs (e.g. SalesInvoiceDetails → SalesInvoices) stay valid.
        await db.Set<T>().AddRangeAsync(rows, ct);
        foreach (var entry in db.ChangeTracker.Entries<T>())
        {
            entry.State = EntityState.Added;
            foreach (var prop in entry.Properties)
            {
                if (prop.Metadata.IsPrimaryKey() && prop.CurrentValue is not null)
                {
                    prop.IsTemporary = false;
                }
            }
        }

        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>
    /// EnsureCreated does not alter existing SQLite files. Add any model columns that are missing.
    /// </summary>
    private static async Task EnsureLocalSqliteColumnsAsync(POSDbContext db, CancellationToken ct)
    {
        await db.Database.OpenConnectionAsync(ct);
        foreach (var entityType in db.Model.GetEntityTypes())
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrWhiteSpace(tableName) || entityType.IsOwned())
            {
                continue;
            }

            var existing = await GetSqliteColumnsAsync(db, tableName, ct);
            if (existing.Count == 0)
            {
                // Table not present yet; EnsureCreated / EnsureOnAsync handles create.
                continue;
            }

            foreach (var property in entityType.GetProperties())
            {
                if (property.IsPrimaryKey() || property.IsShadowProperty())
                {
                    continue;
                }

                var columnName = property.GetColumnName(
                    Microsoft.EntityFrameworkCore.Metadata.StoreObjectIdentifier.Table(tableName, entityType.GetSchema()));
                if (string.IsNullOrWhiteSpace(columnName)
                    || existing.Contains(columnName))
                {
                    continue;
                }

                var sqliteType = MapClrToSqliteType(property.ClrType);
                var nullable = property.IsNullable || Nullable.GetUnderlyingType(property.ClrType) is not null;
                var defaultClause = nullable
                    ? "NULL"
                    : sqliteType switch
                    {
                        "INTEGER" => "NOT NULL DEFAULT 0",
                        "REAL" => "NOT NULL DEFAULT 0",
                        "TEXT" => "NOT NULL DEFAULT ''",
                        _ => "NULL"
                    };

                var sql = $"ALTER TABLE \"{tableName}\" ADD COLUMN \"{columnName}\" {sqliteType} {defaultClause}";
                await db.Database.ExecuteSqlRawAsync(sql, ct);
                existing.Add(columnName);
            }
        }
    }

    private static async Task<HashSet<string>> GetSqliteColumnsAsync(
        POSDbContext db,
        string table,
        CancellationToken ct)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var conn = db.Database.GetDbConnection();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{table}')";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var name = reader["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name))
            {
                cols.Add(name);
            }
        }

        return cols;
    }

    private static string MapClrToSqliteType(Type clrType)
    {
        var t = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (t == typeof(string) || t == typeof(Guid) || t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(byte[]))
        {
            return "TEXT";
        }

        if (t == typeof(bool) || t == typeof(byte) || t == typeof(short) || t == typeof(int) || t == typeof(long))
        {
            return "INTEGER";
        }

        if (t == typeof(float) || t == typeof(double) || t == typeof(decimal))
        {
            return "REAL";
        }

        return "TEXT";
    }

    private static string FlattenDbError(Exception ex)
    {
        var parts = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (!string.IsNullOrWhiteSpace(e.Message))
            {
                parts.Add(e.Message.Trim());
            }
        }

        return string.Join(" → ", parts.Distinct(StringComparer.Ordinal));
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
                CustomerGivenAmount = localInvoice.CustomerGivenAmount,
                ChangeGivenAmount = localInvoice.ChangeGivenAmount,
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
            centralInvoice.CustomerGivenAmount = localInvoice.CustomerGivenAmount;
            centralInvoice.ChangeGivenAmount = localInvoice.ChangeGivenAmount;
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
