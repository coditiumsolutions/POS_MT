using POS_MT.ViewModels;

namespace POS_MT.Interfaces;

public interface IStockMovementService
{
    Task<StockMovementIndexViewModel> AnalyzeAsync(string? classificationFilter = null, CancellationToken cancellationToken = default);
}
