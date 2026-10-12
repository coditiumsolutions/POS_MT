/* Stage 2 schema for LocalHub sync — safe to run multiple times.
   1) ClientTransactionId on SalesInvoices for idempotent sync
   2) SyncOutbox queue with Pending/Syncing/Synced/Failed
*/

IF COL_LENGTH('dbo.SalesInvoices', 'ClientTransactionId') IS NULL
BEGIN
    ALTER TABLE dbo.SalesInvoices ADD ClientTransactionId uniqueidentifier NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_SalesInvoices_ClientTransactionId'
      AND object_id = OBJECT_ID('dbo.SalesInvoices')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_SalesInvoices_ClientTransactionId
        ON dbo.SalesInvoices(ClientTransactionId)
        WHERE ClientTransactionId IS NOT NULL;
END
GO

IF OBJECT_ID('dbo.SyncOutbox', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SyncOutbox
    (
        uid int IDENTITY(1,1) NOT NULL CONSTRAINT PK_SyncOutbox PRIMARY KEY,
        EntityType nvarchar(50) NOT NULL,
        EntityUid int NOT NULL,
        ClientTransactionId uniqueidentifier NOT NULL,
        Status nvarchar(20) NOT NULL CONSTRAINT DF_SyncOutbox_Status DEFAULT ('Pending'),
        AttemptCount int NOT NULL CONSTRAINT DF_SyncOutbox_AttemptCount DEFAULT (0),
        LastError nvarchar(2000) NULL,
        CreatedDate datetime2(0) NOT NULL CONSTRAINT DF_SyncOutbox_CreatedDate DEFAULT (sysdatetime()),
        UpdatedDate datetime2(0) NULL,
        SyncedDate datetime2(0) NULL
    );

    CREATE UNIQUE INDEX UX_SyncOutbox_ClientTransactionId
        ON dbo.SyncOutbox(ClientTransactionId);

    CREATE INDEX IX_SyncOutbox_Status_CreatedDate
        ON dbo.SyncOutbox(Status, CreatedDate);
END
GO
