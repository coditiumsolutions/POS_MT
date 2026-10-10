IF OBJECT_ID(N'dbo.CustomerMonthlyItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerMonthlyItems
    (
        [uid] INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CustomerMonthlyItems PRIMARY KEY,
        [CustomerCode] NVARCHAR(50) NOT NULL,
        [CustomerName] NVARCHAR(200) NOT NULL,
        [ProductCode] NVARCHAR(50) NOT NULL,
        [ProductName] NVARCHAR(200) NOT NULL,
        [BrandName] NVARCHAR(100) NULL,
        [Quantity] DECIMAL(18, 3) NOT NULL CONSTRAINT DF_CustomerMonthlyItems_Quantity DEFAULT (0),
        [UnitPrice] DECIMAL(18, 2) NOT NULL CONSTRAINT DF_CustomerMonthlyItems_UnitPrice DEFAULT (0),
        [CustomerUID] INT NULL,
        [ProductUID] INT NULL,
        [CreatedDate] DATETIME2(0) NOT NULL CONSTRAINT DF_CustomerMonthlyItems_CreatedDate DEFAULT (SYSDATETIME()),
        [UpdatedDate] DATETIME2(0) NULL
    );

    CREATE INDEX IX_CustomerMonthlyItems_CustomerCode ON dbo.CustomerMonthlyItems ([CustomerCode]);
    CREATE INDEX IX_CustomerMonthlyItems_ProductCode ON dbo.CustomerMonthlyItems ([ProductCode]);
    CREATE INDEX IX_CustomerMonthlyItems_CustomerUID ON dbo.CustomerMonthlyItems ([CustomerUID]);
END
GO
