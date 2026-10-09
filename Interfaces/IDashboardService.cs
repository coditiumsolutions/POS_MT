using POS_MT.ViewModels;

namespace POS_MT.Interfaces;

public interface IDashboardService
{
    Task<DashboardViewModel> GetSnapshotAsync(int? itemMonth = null, CancellationToken cancellationToken = default);
}
