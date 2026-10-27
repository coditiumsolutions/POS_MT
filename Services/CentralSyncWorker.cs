using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS_MT.Data;
using POS_MT.Models;
using POS_MT.Options;

namespace POS_MT.Services;

/// <summary>
/// Pushes local SyncOutbox SalesInvoice rows to the central SQL database when reachable.
/// Never deletes pending rows; never overwrites an existing central invoice.
/// </summary>
public sealed class CentralSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly IOptions<PosHubOptions> _options;
    private readonly HubSyncStateStore _state;
    private readonly ILogger<CentralSyncWorker> _logger;

    public CentralSyncWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        IOptions<PosHubOptions> options,
        HubSyncStateStore state,
        ILogger<CentralSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _options = options;
        _state = state;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.Value;
            var delay = TimeSpan.FromSeconds(Math.Max(10, opts.SyncIntervalSeconds));

            try
            {
                if (opts.SyncEnabled)
                {
                    await ProcessBatchAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Central sync worker loop failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var workScope = _scopeFactory.CreateScope();
        var router = workScope.ServiceProvider.GetRequiredService<IPosConnectionRouter>();
        var outboxService = workScope.ServiceProvider.GetRequiredService<ISyncOutboxService>();

        var sqlitePaths = workScope.ServiceProvider.GetRequiredService<IOfflineSqlitePaths>();
        if (!sqlitePaths.FileExists())
        {
            await outboxService.RefreshStatusCountsAsync(ct);
            return;
        }

        var localCs = sqlitePaths.GetConnectionString();
        var centralCs = router.GetOnlineConnectionString()
            ?? _configuration.GetConnectionString("POS_MT_Central")
            ?? _configuration.GetConnectionString("POS_MT");

        if (string.IsNullOrWhiteSpace(localCs) || string.IsNullOrWhiteSpace(centralCs)
            || centralCs.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            await outboxService.RefreshStatusCountsAsync(ct);
            return;
        }

        var localOptions = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlite(localCs)
            .Options;
        await using var localDb = new POSDbContext(localOptions);

        var batch = await localDb.SyncOutboxes
            .Where(x => x.Status == SyncOutbox.StatusPending || x.Status == SyncOutbox.StatusFailed)
            .Where(x => x.AttemptCount < 10)
            .OrderBy(x => x.CreatedDate)
            .Take(20)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            await outboxService.RefreshStatusCountsAsync(ct);
            return;
        }

        _state.Report("Syncing", batch.Count, await localDb.SyncOutboxes.CountAsync(x => x.Status == SyncOutbox.StatusFailed, ct));

        var centralOptions = new DbContextOptionsBuilder<POSDbContext>()
            .UseSqlServer(centralCs)
            .Options;

        await using var centralDb = new POSDbContext(centralOptions);

        try
        {
            if (!await centralDb.Database.CanConnectAsync(ct))
            {
                _logger.LogWarning("Central database unreachable; leaving outbox pending.");
                await outboxService.RefreshStatusCountsAsync(ct);
                return;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Central database connectivity check failed.");
            await outboxService.RefreshStatusCountsAsync(ct);
            return;
        }

        foreach (var item in batch)
        {
            ct.ThrowIfCancellationRequested();
            item.Status = SyncOutbox.StatusSyncing;
            item.AttemptCount += 1;
            item.UpdatedDate = DateTime.Now;
            await localDb.SaveChangesAsync(ct);

            try
            {
                await PushSalesInvoiceAsync(localDb, centralDb, item, ct);
                item.Status = SyncOutbox.StatusSynced;
                item.SyncedDate = DateTime.Now;
                item.LastError = null;
                item.UpdatedDate = DateTime.Now;
                await localDb.SaveChangesAsync(ct);
                _logger.LogInformation("Synced SalesInvoice {EntityUid} ({ClientTx})", item.EntityUid, item.ClientTransactionId);
            }
            catch (Exception ex)
            {
                item.Status = SyncOutbox.StatusFailed;
                item.LastError = ex.Message.Length > 1900 ? ex.Message[..1900] : ex.Message;
                item.UpdatedDate = DateTime.Now;
                await localDb.SaveChangesAsync(ct);
                _logger.LogWarning(ex, "Failed syncing outbox {OutboxUid}", item.Uid);
            }
        }

        await outboxService.RefreshStatusCountsAsync(ct);
    }

    private static async Task PushSalesInvoiceAsync(
        POSDbContext localDb,
        POSDbContext centralDb,
        SyncOutbox item,
        CancellationToken ct)
    {
        var localInvoice = await localDb.SalesInvoices.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Uid == item.EntityUid, ct)
            ?? throw new InvalidOperationException($"Local sales invoice {item.EntityUid} not found.");

        var details = await localDb.SalesInvoiceDetails.AsNoTracking()
            .Where(x => x.SalesInvoiceUid == localInvoice.Uid)
            .OrderBy(x => x.Uid)
            .ToListAsync(ct);

        await using var tx = await centralDb.Database.BeginTransactionAsync(ct);

        var centralInvoice = await centralDb.SalesInvoices
            .FirstOrDefaultAsync(x => x.ClientTransactionId == item.ClientTransactionId, ct);

        if (centralInvoice is null)
        {
            var invoiceNoClash = await centralDb.SalesInvoices.AsNoTracking()
                .AnyAsync(x => x.InvoiceNo == localInvoice.InvoiceNo, ct);
            if (invoiceNoClash)
            {
                throw new InvalidOperationException(
                    $"Central already has InvoiceNo '{localInvoice.InvoiceNo}' with a different ClientTransactionId. Not overwriting.");
            }

            centralInvoice = new SalesInvoice
            {
                ClientTransactionId = item.ClientTransactionId,
                InvoiceNo = localInvoice.InvoiceNo,
                InvoiceDate = localInvoice.InvoiceDate,
                BranchUid = localInvoice.BranchUid,
                CustomerUid = localInvoice.CustomerUid,
                UserUid = localInvoice.UserUid,
                TerminalUid = localInvoice.TerminalUid,
                PaymentMethodUid = localInvoice.PaymentMethodUid,
                SubTotal = localInvoice.SubTotal,
                DiscountAmount = localInvoice.DiscountAmount,
                TaxAmount = localInvoice.TaxAmount,
                OtherCharges = localInvoice.OtherCharges,
                NetAmount = localInvoice.NetAmount,
                PaidAmount = localInvoice.PaidAmount,
                CustomerGivenAmount = localInvoice.CustomerGivenAmount,
                ChangeGivenAmount = localInvoice.ChangeGivenAmount,
                BalanceAmount = localInvoice.BalanceAmount,
                PaymentStatus = localInvoice.PaymentStatus,
                InvoiceStatus = localInvoice.InvoiceStatus,
                Remarks = localInvoice.Remarks,
                CreatedDate = localInvoice.CreatedDate,
                UpdatedDate = localInvoice.UpdatedDate
            };
            centralDb.SalesInvoices.Add(centralInvoice);
            await centralDb.SaveChangesAsync(ct);
        }
        else
        {
            // Same ClientTransactionId: refresh amounts/status only (never invent a second invoice).
            centralInvoice.SubTotal = localInvoice.SubTotal;
            centralInvoice.DiscountAmount = localInvoice.DiscountAmount;
            centralInvoice.TaxAmount = localInvoice.TaxAmount;
            centralInvoice.OtherCharges = localInvoice.OtherCharges;
            centralInvoice.NetAmount = localInvoice.NetAmount;
            centralInvoice.PaidAmount = localInvoice.PaidAmount;
            centralInvoice.CustomerGivenAmount = localInvoice.CustomerGivenAmount;
            centralInvoice.ChangeGivenAmount = localInvoice.ChangeGivenAmount;
            centralInvoice.BalanceAmount = localInvoice.BalanceAmount;
            centralInvoice.PaymentStatus = localInvoice.PaymentStatus;
            centralInvoice.InvoiceStatus = localInvoice.InvoiceStatus;
            centralInvoice.Remarks = localInvoice.Remarks;
            centralInvoice.UpdatedDate = DateTime.Now;

            var oldDetails = await centralDb.SalesInvoiceDetails
                .Where(x => x.SalesInvoiceUid == centralInvoice.Uid)
                .ToListAsync(ct);
            if (oldDetails.Count > 0)
            {
                centralDb.SalesInvoiceDetails.RemoveRange(oldDetails);
            }
        }

        foreach (var d in details)
        {
            centralDb.SalesInvoiceDetails.Add(new SalesInvoiceDetail
            {
                SalesInvoiceUid = centralInvoice.Uid,
                ProductUid = d.ProductUid,
                ProductCode = d.ProductCode,
                ProductName = d.ProductName,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                DiscountPercent = d.DiscountPercent,
                DiscountAmount = d.DiscountAmount,
                TaxPercent = d.TaxPercent,
                TaxAmount = d.TaxAmount,
                LineTotal = d.LineTotal,
                CostPrice = d.CostPrice,
                Remarks = d.Remarks
            });
        }

        await centralDb.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
