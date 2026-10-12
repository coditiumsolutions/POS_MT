using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using POS_MT.Options;
using POS_MT.Services;

namespace POS_MT.Controllers;

[Authorize]
[ApiController]
[Route("api/hub")]
public sealed class HubStatusController : ControllerBase
{
    private readonly IHubStatusService _hubStatus;
    private readonly IPosConnectionRouter _router;
    private readonly PosHubOptions _options;

    public HubStatusController(
        IHubStatusService hubStatus,
        IPosConnectionRouter router,
        IOptions<PosHubOptions> options)
    {
        _hubStatus = hubStatus;
        _router = router;
        _options = options.Value;
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct)
    {
        var status = await _hubStatus.GetStatusAsync(ct);
        return Ok(new
        {
            status.Mode,
            status.ShopName,
            status.WorkMode,
            status.ActiveDatabase,
            status.LocalDatabaseOk,
            status.LocalConfigured,
            status.LocalDatabaseMessage,
            status.CentralConfigured,
            status.CentralReachable,
            status.CentralMessage,
            status.Connectivity,
            status.SyncStatus,
            status.PendingCount,
            status.FailedCount,
            status.RoutingMessage,
            status.CheckedAt,
            pollSeconds = Math.Max(5, _options.StatusPollSeconds)
        });
    }

    public sealed class WorkModeRequest
    {
        public string Mode { get; set; } = "Auto";
    }

    /// <summary>
    /// Switch work mode: Auto | Online | Offline.
    /// Offline uses local SQLite (created via: POS_MT.exe offline pull).
    /// Persisted under App_Data/pos-work-mode.json.
    /// </summary>
    [HttpPost("work-mode")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetWorkMode([FromBody] WorkModeRequest request, CancellationToken ct)
    {
        if (request is null || !Enum.TryParse<PosWorkMode>(request.Mode, true, out var mode))
        {
            return BadRequest(new { success = false, message = "Mode must be Auto, Online, or Offline." });
        }

        if (mode == PosWorkMode.Offline)
        {
            await _router.RefreshAsync(ct);
            var snap = _router.Snapshot();
            if (!snap.LocalReachable)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Cannot go Offline: local SQLite is missing or unreadable. On this machine run: POS_MT.exe offline pull (or: dotnet run -- offline pull)."
                });
            }
        }

        _router.SetWorkMode(mode);
        await _router.RefreshAsync(ct);
        var status = await _hubStatus.GetStatusAsync(ct);

        if (mode == PosWorkMode.Offline
            && !string.Equals(status.ActiveDatabase, "Local", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                success = false,
                message = status.RoutingMessage
            });
        }

        return Ok(new
        {
            success = true,
            message = status.RoutingMessage,
            status.WorkMode,
            status.ActiveDatabase,
            status.Connectivity
        });
    }
}
