/* Add AdvanceBalance to Customers + AppliedToInvoiceAmount to CustomerPayments (safe to run multiple times). */
IF OBJECT_ID(N'dbo.Customers', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Customers', N'AdvanceBalance') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD [AdvanceBalance] DECIMAL(18, 2) NOT NULL
            CONSTRAINT DF_Customers_AdvanceBalance DEFAULT (0);
END
GO

IF OBJECT_ID(N'dbo.CustomerPayments', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerPayments', N'AppliedToInvoiceAmount') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerPayments
        ADD [AppliedToInvoiceAmount] DECIMAL(18, 2) NOT NULL
            CONSTRAINT DF_CustomerPayments_AppliedToInvoiceAmount DEFAULT (0);
END
GO
