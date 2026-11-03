using Microsoft.EntityFrameworkCore;
using POS_MT.Data;

namespace POS_MT.Services;

public interface ISalesInvoiceSchemaService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}

/// <summary>Ensures SalesInvoices stamp columns exist on SQL Server and SQLite.</summary>
public sealed class SalesInvoiceSchemaService : ISalesInvoiceSchemaService
{
    private readonly POSDbContext _db;
    private bool _ready;

    public SalesInvoiceSchemaService(POSDbContext db) => _db = db;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_ready)
            return;

        try
        {
            if (_db.Database.IsSqlite())
            {
                await EnsureSqliteColumnAsync("SalesInvoices", "UserName",
                    "ALTER TABLE SalesInvoices ADD COLUMN UserName TEXT NULL", cancellationToken);
                await EnsureSqliteColumnAsync("SalesInvoices", "Branch",
                    "ALTER TABLE SalesInvoices ADD COLUMN Branch TEXT NULL", cancellationToken);
                await EnsureSqliteColumnAsync("SalesInvoices", "Terminal",
                    "ALTER TABLE SalesInvoices ADD COLUMN Terminal TEXT NULL", cancellationToken);
                await EnsureSqliteColumnAsync("SalesInvoices", "CustomerGivenAmount",
                    "ALTER TABLE SalesInvoices ADD COLUMN CustomerGivenAmount REAL NULL", cancellationToken);
                await EnsureSqliteColumnAsync("SalesInvoices", "ChangeGivenAmount",
                    "ALTER TABLE SalesInvoices ADD COLUMN ChangeGivenAmount REAL NULL", cancellationToken);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.SalesInvoices', N'UserName') IS NULL
                        ALTER TABLE dbo.SalesInvoices ADD [UserName] NVARCHAR(100) NULL;
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.SalesInvoices', N'Branch') IS NULL
                        ALTER TABLE dbo.SalesInvoices ADD [Branch] NVARCHAR(150) NULL;
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.SalesInvoices', N'Terminal') IS NULL
                        ALTER TABLE dbo.SalesInvoices ADD [Terminal] NVARCHAR(100) NULL;
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.SalesInvoices', N'CustomerGivenAmount') IS NULL
                        ALTER TABLE dbo.SalesInvoices ADD [CustomerGivenAmount] DECIMAL(18, 2) NULL;
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    IF COL_LENGTH(N'dbo.SalesInvoices', N'ChangeGivenAmount') IS NULL
                        ALTER TABLE dbo.SalesInvoices ADD [ChangeGivenAmount] DECIMAL(18, 2) NULL;
                    """, cancellationToken);
            }

            _ready = true;
        }
        catch
        {
            // Leave _ready false so a later request retries.
        }
    }

    private async Task EnsureSqliteColumnAsync(string table, string column, string alterSql, CancellationToken ct)
    {
        var conn = _db.Database.GetDbConnection();
        await _db.Database.OpenConnectionAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({table})";
        var found = false;
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                if (string.Equals(reader["name"]?.ToString(), column, StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
        }

        if (!found)
            await _db.Database.ExecuteSqlRawAsync(alterSql, ct);
    }
}
