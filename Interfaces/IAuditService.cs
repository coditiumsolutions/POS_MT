namespace POS_MT.Interfaces;

public interface IAuditService
{
    Task WriteAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        CancellationToken cancellationToken = default);
}
