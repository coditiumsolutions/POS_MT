IF COL_LENGTH('dbo.Products', 'ImagePath') IS NULL
BEGIN
    ALTER TABLE dbo.Products ADD ImagePath nvarchar(500) NULL;
END
GO
