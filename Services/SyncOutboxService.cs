using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using POS_MT.Data;
using POS_MT.Models;
using POS_MT.Options;

namespace POS_MT.Services;

public interface ISyncOutboxService
{
    Task EnqueueSalesInvoiceAsync(int salesInvoiceUid, Guid clientTransactionId, CancellationToken ct = default);
    Task RefreshStatusCountsAsync(CancellationToken ct = default);
}

public sealed class SyncOutboxService : ISyncOutboxService
{
    private readonly POSDbContext _db;
    private readonly PosHubOptions _options;
    private readonly HubSyncStateStore _state;
    private readonly IPosConnectionRouter _router;

    public SyncOutboxService(
        POSDbContext db,
        IOptions<PosHubOptions> options,
        HubSyncStateStore state,
        IPosConnectionRouter router)
    {
        _db = db;
        _options = options.Value;
        _state = state;
        _router = router;
    }

    public async Task EnqueueSalesInvoiceAsync(int salesInvoiceUid, Guid clientTransactionId, CancellationToken ct = default)
    {
        if (!_options.SyncEnabled)
        {
            return;
        }

        // Queue only when the active write target is local SQLite (not Mode Online / central).
        var onLocal = string.Equals(_router.ActiveTarget, "Local", StringComparison.OrdinalIgnoreCase);
        if (!onLocal)
        {
            return;
        }

        var existing = await _db.SyncOutboxes
            .FirstOrDefaultAsync(x => x.ClientTransactionId == clientTransactionId, ct);

        if (existing is not null)
        {
            existing.EntityUid = salesInvoiceUid;
            // Re-queue so central receives updated draft lines / totals for the same ClientTransactionId.
            if (existing.Status != SyncOutbox.StatusPending && existing.Status != SyncOutbox.StatusSyncing)
            {
                existing.Status = SyncOutbox.StatusPending;
                existing.LastError = null;
                existing.UpdatedDate = DateTime.Now;
                await _db.SaveChangesAsync(ct);
            }

            await RefreshStatusCountsAsync(ct);
            return;
        }

        _db.SyncOutboxes.Add(new SyncOutbox
        {
            EntityType = "SalesInvoice",
            EntityUid = salesInvoiceUid,
            ClientTransactionId = clientTransactionId,
            Status = SyncOutbox.StatusPending,
            AttemptCount = 0,
            CreatedDate = DateTime.Now
        });
        await _db.SaveChangesAsync(ct);
        await RefreshStatusCountsAsync(ct);
    }

    public async Task RefreshStatusCountsAsync(CancellationToken ct = default)
    {
        var onLocal = string.Equals(_router.ActiveTarget, "Local", StringComparison.OrdinalIgnoreCase);
        if (!onLocal)
        {
            _state.Report("Synced", 0, 0);
            return;
        }

        try
        {
            var pending = await _db.SyncOutboxes.CountAsync(
                x => x.Status == SyncOutbox.StatusPending || x.Status == SyncOutbox.StatusSyncing, ct);
            var failed = await _db.SyncOutboxes.CountAsync(x => x.Status == SyncOutbox.StatusFailed, ct);
            var status = failed > 0 ? "Sync Failed" : pending > 0 ? "Pending" : "Synced";
            _state.Report(status, pending, failed);
        }
        catch
        {
            _state.Report("Idle", 0, 0);
        }
    }
}
