/* Rename CustomerMonthlyItems → CustomerMonthlySupply (safe to run multiple times). */
IF OBJECT_ID(N'dbo.CustomerMonthlyItems', N'U') IS NOT NULL
   AND OBJECT_ID(N'dbo.CustomerMonthlySupply', N'U') IS NULL
BEGIN
    EXEC sp_rename N'dbo.CustomerMonthlyItems', N'CustomerMonthlySupply';
END
GO

IF OBJECT_ID(N'dbo.CustomerMonthlySupply', N'U') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = N'PK_CustomerMonthlyItems' AND parent_object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.PK_CustomerMonthlyItems', N'PK_CustomerMonthlySupply', N'OBJECT';

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerMonthlyItems_CustomerCode' AND object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.CustomerMonthlySupply.IX_CustomerMonthlyItems_CustomerCode', N'IX_CustomerMonthlySupply_CustomerCode', N'INDEX';

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerMonthlyItems_ProductCode' AND object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.CustomerMonthlySupply.IX_CustomerMonthlyItems_ProductCode', N'IX_CustomerMonthlySupply_ProductCode', N'INDEX';

    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerMonthlyItems_CustomerUID' AND object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.CustomerMonthlySupply.IX_CustomerMonthlyItems_CustomerUID', N'IX_CustomerMonthlySupply_CustomerUID', N'INDEX';

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_CustomerMonthlyItems_Quantity' AND parent_object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.DF_CustomerMonthlyItems_Quantity', N'DF_CustomerMonthlySupply_Quantity', N'OBJECT';

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_CustomerMonthlyItems_UnitPrice' AND parent_object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.DF_CustomerMonthlyItems_UnitPrice', N'DF_CustomerMonthlySupply_UnitPrice', N'OBJECT';

    IF EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_CustomerMonthlyItems_CreatedDate' AND parent_object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply'))
        EXEC sp_rename N'dbo.DF_CustomerMonthlyItems_CreatedDate', N'DF_CustomerMonthlySupply_CreatedDate', N'OBJECT';
END
GO

IF OBJECT_ID(N'dbo.CustomerMonthlySupply', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerMonthlySupply
    (
        [uid] INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerMonthlySupply PRIMARY KEY,
        [CustomerCode] NVARCHAR(50) NOT NULL,
        [CustomerName] NVARCHAR(200) NOT NULL,
        [ProductCode] NVARCHAR(50) NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [BrandName] NVARCHAR(100) NULL,
        [Quantity] DECIMAL(18, 3) NOT NULL CONSTRAINT DF_CustomerMonthlySupply_Quantity DEFAULT (0),
        [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CustomerMonthlySupply_UnitPrice DEFAULT (0),
        [DeliveryCharges] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CustomerMonthlySupply_DeliveryCharges DEFAULT (0),
        [CustomerUID] INT NULL,
        [ProductUID] INT NULL,
        [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerMonthlySupply_CreatedDate DEFAULT (SYSDATETIME()),
        [UpdatedDate] DATETIME2(0) NULL
    );

    CREATE INDEX IX_CustomerMonthlySupply_CustomerCode ON dbo.CustomerMonthlySupply ([CustomerCode]);
    CREATE INDEX IX_CustomerMonthlySupply_ProductCode ON dbo.CustomerMonthlySupply ([ProductCode]);
    CREATE INDEX IX_CustomerMonthlySupply_CustomerUID ON dbo.CustomerMonthlySupply ([CustomerUID]);
END
GO
