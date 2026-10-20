/* Add Branch and Terminal name columns to SalesInvoices (safe to run multiple times). */
IF OBJECT_ID(N'dbo.SalesInvoices', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SalesInvoices', N'Branch') IS NULL
    BEGIN
        ALTER TABLE dbo.SalesInvoices ADD [Branch] NVARCHAR(150) NULL;
    END

    IF COL_LENGTH(N'dbo.SalesInvoices', N'Terminal') IS NULL
    BEGIN
        ALTER TABLE dbo.SalesInvoices ADD [Terminal] NVARCHAR(100) NULL;
    END
END
GO
