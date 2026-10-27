using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using POS_MT.Interfaces;
using POS_MT.Services;
using POS_MT.ViewModels;

namespace POS_MT.Controllers;

[Authorize(Policy = "AdminOnly")]
public class OfflineSyncTablesController : Controller
{
    private readonly IOfflineSyncCatalogService _catalog;
    private readonly IOfflineSqliteSyncService _sync;
    private readonly INavContextService _navContext;
    private readonly IAuditService _audit;

    public OfflineSyncTablesController(
        IOfflineSyncCatalogService catalog,
        IOfflineSqliteSyncService sync,
        INavContextService navContext,
        IAuditService audit)
    {
        _catalog = catalog;
        _sync = sync;
        _navContext = navContext;
        _audit = audit;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        _navContext.SetArea("AllModules");
        ViewData["Title"] = "Sync Tables";
        var rows = await _catalog.GetAllAsync(ct);
        var model = new OfflineSyncTablesPageViewModel
        {
            Rows = rows.Select(x => new OfflineSyncTableRowViewModel
            {
                Uid = x.Uid,
                TableName = x.TableName,
                DisplayName = x.DisplayName,
                PullFromOnline = x.PullFromOnline,
                PushToOnline = x.PushToOnline,
                SyncMode = x.SyncMode,
                SortOrder = x.SortOrder,
                Notes = x.Notes,
                IsActive = x.IsActive,
                UpdatedDate = x.UpdatedDate
            }).ToList()
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(OfflineSyncTablesPageViewModel model, CancellationToken ct)
    {
        _navContext.SetArea("AllModules");
        if (model.Rows is null || model.Rows.Count == 0)
        {
            TempData["Error"] = "No sync table rows posted.";
            return RedirectToAction(nameof(Index));
        }

        foreach (var row in model.Rows)
        {
            await _catalog.UpdateFlagsAsync(row.Uid, row.PullFromOnline, row.PushToOnline, row.IsActive, ct);
        }

        await _audit.WriteAsync("Update", "OfflineSyncTables", "bulk",
            $"Updated pull/push flags for {model.Rows.Count} tables.", ct);
        TempData["Success"] = "Settings saved. Click Pull now while Online to copy checked tables into local SQLite, then go Offline.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Copies PullFromOnline tables from central SQL into local SQLite. Must run while Online.
    /// Going Offline does not pull automatically.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PullNow(CancellationToken ct)
    {
        _navContext.SetArea("AllModules");
        try
        {
            var result = await _sync.PullAsync(ct);
            await _audit.WriteAsync("Sync", "OfflineSyncTables", "pull", result, ct);
            TempData["Success"] = result;
        }
        catch (Exception ex)
        {
            var parts = new List<string>();
            for (var e = ex; e is not null; e = e.InnerException)
            {
                if (!string.IsNullOrWhiteSpace(e.Message))
                {
                    parts.Add(e.Message.Trim());
                }
            }

            TempData["Error"] = string.Join(" → ", parts.Distinct(StringComparer.Ordinal));
        }

        return RedirectToAction(nameof(Index));
    }
}
