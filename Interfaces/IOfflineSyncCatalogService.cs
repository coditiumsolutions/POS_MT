using POS_MT.Models;

namespace POS_MT.Interfaces;

public interface IOfflineSyncCatalogService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<List<OfflineSyncTable>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetPullTableNamesAsync(CancellationToken cancellationToken = default);

    Task UpdateFlagsAsync(int uid, bool pullFromOnline, bool pushToOnline, bool isActive, CancellationToken cancellationToken = default);
}
