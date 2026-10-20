/* Add DeliveryCharges to CustomerMonthlySupply (safe to run multiple times). */
IF OBJECT_ID(N'dbo.CustomerMonthlySupply', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerMonthlySupply', N'DeliveryCharges') IS NULL
BEGIN
    ALTER TABLE dbo.CustomerMonthlySupply
        ADD [DeliveryCharges] DECIMAL(18, 2) NULL
            CONSTRAINT DF_CustomerMonthlySupply_DeliveryCharges DEFAULT (0);
END
GO

IF OBJECT_ID(N'dbo.CustomerMonthlySupply', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.CustomerMonthlySupply', N'DeliveryCharges') IS NOT NULL
BEGIN
    UPDATE dbo.CustomerMonthlySupply
    SET [DeliveryCharges] = 0
    WHERE [DeliveryCharges] IS NULL;

    IF EXISTS (
        SELECT 1
        FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.CustomerMonthlySupply')
          AND name = N'DeliveryCharges'
          AND is_nullable = 1
    )
    BEGIN
        ALTER TABLE dbo.CustomerMonthlySupply
            ALTER COLUMN [DeliveryCharges] DECIMAL(18, 2) NOT NULL;
    END
END
GO
