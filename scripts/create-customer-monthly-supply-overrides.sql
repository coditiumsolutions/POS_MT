/* Day-specific monthly supply overrides. Safe to run multiple times. */
IF OBJECT_ID(N'dbo.CustomerMonthlySupplyOverrides', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerMonthlySupplyOverrides
    (
        [uid] INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerMonthlySupplyOverrides PRIMARY KEY,
        [CustomerUID] INT NOT NULL,
        [ProductUID] INT NOT NULL,
        [CustomerCode] NVARCHAR(50) NOT NULL,
        [ProductCode] NVARCHAR(50) NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [BrandName] NVARCHAR(100) NULL,
        [SupplyDate] DATE NOT NULL,
        [Quantity] DECIMAL(18, 3) NOT NULL CONSTRAINT DF_CustomerMonthlySupplyOverrides_Quantity DEFAULT (0),
        [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CustomerMonthlySupplyOverrides_UnitPrice DEFAULT (0),
        [Remarks] NVARCHAR(500) NULL,
        [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerMonthlySupplyOverrides_CreatedDate DEFAULT (SYSDATETIME()),
        [UpdatedDate] DATETIME2(0) NULL
    );

    CREATE UNIQUE INDEX UX_CustomerMonthlySupplyOverrides_Customer_Product_Date
        ON dbo.CustomerMonthlySupplyOverrides ([CustomerUID], [ProductUID], [SupplyDate]);
    CREATE INDEX IX_CustomerMonthlySupplyOverrides_SupplyDate
        ON dbo.CustomerMonthlySupplyOverrides ([SupplyDate]);
    CREATE INDEX IX_CustomerMonthlySupplyOverrides_CustomerUID
        ON dbo.CustomerMonthlySupplyOverrides ([CustomerUID]);
END
GO
