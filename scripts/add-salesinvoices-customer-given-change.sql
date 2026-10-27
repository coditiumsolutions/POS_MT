/* Customer tender + change on cash POS sales (safe to run multiple times). */
IF OBJECT_ID(N'dbo.SalesInvoices', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'dbo.SalesInvoices', N'CustomerGivenAmount') IS NULL
    BEGIN
        ALTER TABLE dbo.SalesInvoices ADD [CustomerGivenAmount] DECIMAL(18, 2) NULL;
    END

    IF COL_LENGTH(N'dbo.SalesInvoices', N'ChangeGivenAmount') IS NULL
    BEGIN
        ALTER TABLE dbo.SalesInvoices ADD [ChangeGivenAmount] DECIMAL(18, 2) NULL;
    END
END
GO
