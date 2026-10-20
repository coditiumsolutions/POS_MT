/* Add TerminalName to Users (safe to run multiple times). */
IF OBJECT_ID(N'dbo.Users', N'U') IS NOT NULL
   AND COL_LENGTH(N'dbo.Users', N'TerminalName') IS NULL
BEGIN
    ALTER TABLE dbo.Users
        ADD [TerminalName] NVARCHAR(100) NULL;
END
GO
