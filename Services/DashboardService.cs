using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.ViewModels;

namespace POS_MT.Services;

public sealed class DashboardService : IDashboardService
{
    private readonly POSDbContext _db;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(POSDbContext db, ILogger<DashboardService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DashboardViewModel> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var model = new DashboardViewModel();

        try
        {
            model.DatabaseConnected = await _db.Database.CanConnectAsync(cancellationToken);
            model.StatusMessage = model.DatabaseConnected
                ? "Connected to POS_MT. Module totals will be filled as each feature is implemented."
                : "The application could not open the POS_MT database. Check the server name and that the database exists.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard database check failed.");
            model.DatabaseConnected = false;
            model.StatusMessage = "The application could not open the POS_MT database. Check the server name and that the database exists.";
        }

        return model;
    }
}
