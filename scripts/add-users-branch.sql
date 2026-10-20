/* Add Branch to Users (safe to run multiple times). */
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Users', N'Branch') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD [Branch] NVARCHAR(150) NULL;
END
GO
