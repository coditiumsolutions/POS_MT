namespace POS_MT.Services;

/// <summary>Periodically re-checks online/local SQL reachability for Auto failover.</summary>
public sealed class PosConnectionRefreshService : BackgroundService
{
    private readonly IPosConnectionRouter _router;
    private readonly ILogger<PosConnectionRefreshService> _logger;

    public PosConnectionRefreshService(
        IPosConnectionRouter router,
        ILogger<PosConnectionRefreshService> logger)
    {
        _router = router;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _router.RefreshAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Periodic DB connectivity refresh failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
