using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class OfflineSyncCatalogService : IOfflineSyncCatalogService
{
    private readonly POSDbContext _db;
    private bool _schemaChecked;

    public OfflineSyncCatalogService(POSDbContext db)
    {
        _db = db;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_schemaChecked)
        {
            return;
        }

        await EnsureOnAsync(_db, cancellationToken);
        _schemaChecked = true;
    }

    public async Task<List<OfflineSyncTable>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        return await _db.OfflineSyncTables.AsNoTracking()
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public async Task<HashSet<string>> GetPullTableNamesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        return await GetPullNamesOnAsync(_db, cancellationToken);
    }

    public async Task UpdateFlagsAsync(int uid, bool pullFromOnline, bool pushToOnline, bool isActive, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken);
        var row = await _db.OfflineSyncTables.FirstOrDefaultAsync(x => x.Uid == uid, cancellationToken)
            ?? throw new InvalidOperationException("Sync table row was not found.");
        row.PullFromOnline = pullFromOnline;
        row.PushToOnline = pushToOnline;
        row.IsActive = isActive;
        row.UpdatedDate = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public static async Task EnsureOnAsync(POSDbContext db, CancellationToken cancellationToken = default)
    {
        if (db.Database.IsSqlite())
        {
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS OfflineSyncTables (
                    uid INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                    TableName TEXT NOT NULL,
                    DisplayName TEXT NOT NULL,
                    PullFromOnline INTEGER NOT NULL DEFAULT 0,
                    PushToOnline INTEGER NOT NULL DEFAULT 0,
                    SyncMode TEXT NOT NULL DEFAULT 'Lookup',
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    Notes TEXT NULL,
                    IsActive INTEGER NOT NULL DEFAULT 1,
                    UpdatedDate TEXT NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS UX_OfflineSyncTables_TableName ON OfflineSyncTables(TableName);
                """, cancellationToken);
        }
        else
        {
            await db.Database.ExecuteSqlRawAsync("""
                IF OBJECT_ID(N'dbo.OfflineSyncTables', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.OfflineSyncTables (
                        [uid] INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OfflineSyncTables PRIMARY KEY,
                        [TableName] NVARCHAR(100) NOT NULL,
                        [DisplayName] NVARCHAR(150) NOT NULL,
                        [PullFromOnline] BIT NOT NULL CONSTRAINT DF_OfflineSyncTables_Pull DEFAULT (0),
                        [PushToOnline] BIT NOT NULL CONSTRAINT DF_OfflineSyncTables_Push DEFAULT (0),
                        [SyncMode] NVARCHAR(30) NOT NULL CONSTRAINT DF_OfflineSyncTables_Mode DEFAULT (N'Lookup'),
                        [SortOrder] INT NOT NULL CONSTRAINT DF_OfflineSyncTables_Sort DEFAULT (0),
                        [Notes] NVARCHAR(500) NULL,
                        [IsActive] BIT NOT NULL CONSTRAINT DF_OfflineSyncTables_Active DEFAULT (1),
                        [UpdatedDate] DATETIME2(0) NOT NULL
                    );
                    CREATE UNIQUE INDEX UX_OfflineSyncTables_TableName ON dbo.OfflineSyncTables([TableName]);
                END
                """, cancellationToken);
        }

        await SeedDefaultsOnAsync(db, cancellationToken);
    }

    public static async Task<HashSet<string>> GetPullNamesOnAsync(POSDbContext db, CancellationToken cancellationToken = default)
    {
        var names = await db.OfflineSyncTables.AsNoTracking()
            .Where(x => x.IsActive && x.PullFromOnline)
            .Select(x => x.TableName)
            .ToListAsync(cancellationToken);
        return new HashSet<string>(names, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task SeedDefaultsOnAsync(POSDbContext db, CancellationToken cancellationToken)
    {
        var existing = await db.OfflineSyncTables.AsNoTracking()
            .Select(x => x.TableName)
            .ToListAsync(cancellationToken);
        var known = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);
        var now = DateTime.Now;
        var added = false;
        foreach (var seed in DefaultCatalog(now))
        {
            if (known.Contains(seed.TableName))
            {
                continue;
            }

            db.OfflineSyncTables.Add(seed);
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken);
            db.ChangeTracker.Clear();
        }
    }

    public static List<OfflineSyncTable> DefaultCatalog(DateTime now) =>
    [
        Row("OfflineSyncTables", "Offline Sync Tables", pull: true, push: false, "Lookup", 5, "This catalog (optional pull to local SQLite).", now),
        Row("Products", "Products", pull: true, push: false, "Lookup", 10, "Pulled for Cash POS catalog.", now),
        Row("Inventories", "Inventory", pull: true, push: false, "Lookup", 20, "Stock levels for POS.", now),
        Row("ProductCategories", "Product Categories", pull: true, push: false, "Lookup", 30, null, now),
        Row("ProductUnits", "Product Units", pull: true, push: false, "Lookup", 40, null, now),
        Row("Customers", "Customers", pull: true, push: false, "Lookup", 50, "Needed for credit / monthly POS.", now),
        Row("Users", "Users", pull: true, push: false, "Lookup", 60, "Sign-in when offline.", now),
        Row("Branches", "Branches", pull: true, push: false, "Lookup", 70, null, now),
        Row("Terminals", "Terminals", pull: true, push: false, "Lookup", 80, null, now),
        Row("PaymentMethods", "Payment Methods", pull: true, push: false, "Lookup", 90, null, now),
        Row("Warehouses", "Warehouses", pull: true, push: false, "Lookup", 100, null, now),
        Row("Vendors", "Vendors", pull: true, push: false, "Lookup", 110, null, now),
        Row("Configurations", "Configurations", pull: true, push: false, "Lookup", 120, "Includes POS layout and lists.", now),
        Row("ExpenseCategories", "Expense Categories", pull: true, push: false, "Lookup", 130, null, now),
        Row("SalesInvoices", "Sales Invoices", pull: false, push: true, "Transactional", 200, "Enable Pull, then click Pull now while Online. Replaces local sales (skipped if pending outbox).", now),
        Row("SalesInvoiceDetails", "Sales Invoice Details", pull: false, push: true, "Transactional", 210, "Pulled with Sales Invoices when Pull is enabled.", now),
        Row("SyncOutboxes", "Sync Outbox", pull: false, push: false, "LocalOnly", 220, "Local queue of sales waiting to sync.", now),
        Row("CustomerPayments", "Customer Payments", pull: false, push: false, "Transactional", 300, "Not in offline pull yet.", now),
        Row("CustomerMonthlySupplies", "Monthly Supply", pull: false, push: false, "Lookup", 310, "Enable pull if monthly supply must work offline.", now),
        Row("CustomerMonthlySupplyOverrides", "Monthly Supply Overrides", pull: false, push: false, "Lookup", 320, null, now),
        Row("Expenses", "Expenses", pull: false, push: false, "Transactional", 330, null, now),
        Row("PurchaseInvoices", "Purchase Invoices", pull: false, push: false, "Transactional", 340, null, now),
        Row("PurchaseInvoiceDetails", "Purchase Invoice Details", pull: false, push: false, "Transactional", 350, null, now),
        Row("StockAdjustments", "Stock Adjustments", pull: false, push: false, "Transactional", 360, null, now),
        Row("StockAdjustmentDetails", "Stock Adjustment Details", pull: false, push: false, "Transactional", 370, null, now),
        Row("InventoryTransactions", "Inventory Transactions", pull: false, push: false, "Transactional", 380, null, now),
        Row("VendorPayments", "Vendor Payments", pull: false, push: false, "Transactional", 390, null, now),
        Row("AuditLogs", "Audit Logs", pull: false, push: false, "LocalOnly", 400, "Usually stay on the active database only.", now),
        Row("UserSessions", "User Sessions", pull: false, push: false, "LocalOnly", 410, null, now)
    ];

    private static OfflineSyncTable Row(
        string tableName,
        string displayName,
        bool pull,
        bool push,
        string mode,
        int sort,
        string? notes,
        DateTime now) => new()
    {
        TableName = tableName,
        DisplayName = displayName,
        PullFromOnline = pull,
        PushToOnline = push,
        SyncMode = mode,
        SortOrder = sort,
        Notes = notes,
        IsActive = true,
        UpdatedDate = now
    };
}
