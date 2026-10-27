using Microsoft.EntityFrameworkCore;
using POS_MT.Data;

namespace POS_MT.Services;

public interface IEmployeeSchemaService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);
}

public sealed class EmployeeSchemaService : IEmployeeSchemaService
{
    private readonly POSDbContext _db;
    private bool _checked;

    public EmployeeSchemaService(POSDbContext db) => _db = db;

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        if (_checked)
            return;

        try
        {
            if (_db.Database.IsSqlite())
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS Employees (
                        uid INTEGER PRIMARY KEY AUTOINCREMENT,
                        EmployeeCode TEXT NOT NULL,
                        EmployeeName TEXT NOT NULL,
                        CNIC TEXT NULL,
                        MobileNo1 TEXT NULL,
                        MobileNo2 TEXT NULL,
                        PresentAddress TEXT NULL,
                        PermanentAddress TEXT NULL,
                        TotalSalary REAL NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedDate TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        UpdatedDate TEXT NULL
                    );
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS EmployeeAllowances (
                        uid INTEGER PRIMARY KEY AUTOINCREMENT,
                        EmployeeUID INTEGER NOT NULL,
                        Title TEXT NOT NULL,
                        Amount REAL NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedDate TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS EmployeeDeductions (
                        uid INTEGER PRIMARY KEY AUTOINCREMENT,
                        EmployeeUID INTEGER NOT NULL,
                        Title TEXT NOT NULL,
                        Amount REAL NOT NULL DEFAULT 0,
                        IsActive INTEGER NOT NULL DEFAULT 1,
                        CreatedDate TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    """, cancellationToken);
                await _db.Database.ExecuteSqlRawAsync("""
                    CREATE TABLE IF NOT EXISTS EmployeePayments (
                        uid INTEGER PRIMARY KEY AUTOINCREMENT,
                        PaymentNo TEXT NOT NULL,
                        EmployeeUID INTEGER NOT NULL,
                        PaymentDate TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                        PeriodMonth INTEGER NOT NULL,
                        PeriodYear INTEGER NOT NULL,
                        Amount REAL NOT NULL DEFAULT 0,
                        Remarks TEXT NULL,
                        UserUID INTEGER NULL,
                        CreatedDate TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP
                    );
                    """, cancellationToken);
            }
            else
            {
                await _db.Database.ExecuteSqlRawAsync("""
                    IF OBJECT_ID(N'dbo.Employees', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.Employees (
                            [uid] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [EmployeeCode] NVARCHAR(30) NOT NULL,
                            [EmployeeName] NVARCHAR(200) NOT NULL,
                            [CNIC] NVARCHAR(20) NULL,
                            [MobileNo1] NVARCHAR(30) NULL,
                            [MobileNo2] NVARCHAR(30) NULL,
                            [PresentAddress] NVARCHAR(500) NULL,
                            [PermanentAddress] NVARCHAR(500) NULL,
                            [TotalSalary] DECIMAL(18,2) NOT NULL CONSTRAINT DF_Employees_TotalSalary DEFAULT (0),
                            [IsActive] BIT NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT (1),
                            [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_Employees_CreatedDate DEFAULT (sysdatetime()),
                            [UpdatedDate] DATETIME2(0) NULL
                        );
                        CREATE INDEX IX_Employees_EmployeeCode ON dbo.Employees([EmployeeCode]);
                        CREATE INDEX IX_Employees_CNIC ON dbo.Employees([CNIC]);
                        CREATE INDEX IX_Employees_EmployeeName ON dbo.Employees([EmployeeName]);
                    END
                    """, cancellationToken);

                await _db.Database.ExecuteSqlRawAsync("""
                    IF OBJECT_ID(N'dbo.EmployeeAllowances', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.EmployeeAllowances (
                            [uid] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [EmployeeUID] INT NOT NULL,
                            [Title] NVARCHAR(150) NOT NULL,
                            [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_EmployeeAllowances_Amount DEFAULT (0),
                            [IsActive] BIT NOT NULL CONSTRAINT DF_EmployeeAllowances_IsActive DEFAULT (1),
                            [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_EmployeeAllowances_CreatedDate DEFAULT (sysdatetime())
                        );
                        CREATE INDEX IX_EmployeeAllowances_EmployeeUID ON dbo.EmployeeAllowances([EmployeeUID]);
                    END
                    """, cancellationToken);

                await _db.Database.ExecuteSqlRawAsync("""
                    IF OBJECT_ID(N'dbo.EmployeeDeductions', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.EmployeeDeductions (
                            [uid] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [EmployeeUID] INT NOT NULL,
                            [Title] NVARCHAR(150) NOT NULL,
                            [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_EmployeeDeductions_Amount DEFAULT (0),
                            [IsActive] BIT NOT NULL CONSTRAINT DF_EmployeeDeductions_IsActive DEFAULT (1),
                            [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_EmployeeDeductions_CreatedDate DEFAULT (sysdatetime())
                        );
                        CREATE INDEX IX_EmployeeDeductions_EmployeeUID ON dbo.EmployeeDeductions([EmployeeUID]);
                    END
                    """, cancellationToken);

                await _db.Database.ExecuteSqlRawAsync("""
                    IF OBJECT_ID(N'dbo.EmployeePayments', N'U') IS NULL
                    BEGIN
                        CREATE TABLE dbo.EmployeePayments (
                            [uid] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            [PaymentNo] NVARCHAR(50) NOT NULL,
                            [EmployeeUID] INT NOT NULL,
                            [PaymentDate] DATETIME2(0) NOT NULL CONSTRAINT DF_EmployeePayments_PaymentDate DEFAULT (sysdatetime()),
                            [PeriodMonth] INT NOT NULL,
                            [PeriodYear] INT NOT NULL,
                            [Amount] DECIMAL(18,2) NOT NULL CONSTRAINT DF_EmployeePayments_Amount DEFAULT (0),
                            [Remarks] NVARCHAR(500) NULL,
                            [UserUID] INT NULL,
                            [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_EmployeePayments_CreatedDate DEFAULT (sysdatetime())
                        );
                        CREATE INDEX IX_EmployeePayments_EmployeeUID ON dbo.EmployeePayments([EmployeeUID]);
                        CREATE INDEX IX_EmployeePayments_PaymentDate ON dbo.EmployeePayments([PaymentDate]);
                        CREATE INDEX IX_EmployeePayments_PaymentNo ON dbo.EmployeePayments([PaymentNo]);
                    END
                    """, cancellationToken);
            }
        }
        catch
        {
            // Concurrent create or already exists — continue.
        }

        _checked = true;
    }
}
