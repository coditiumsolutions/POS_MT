using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class AuditService : IAuditService
{
    private readonly POSDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        POSDbContext db,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var http = _httpContextAccessor.HttpContext;
            int? userUid = null;
            var userIdClaim = http?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out var parsedUid))
            {
                userUid = parsedUid;
            }

            int? recordUid = null;
            if (int.TryParse(entityId, out var parsedRecordUid))
            {
                recordUid = parsedRecordUid;
            }

            // Login happens before the auth cookie exists, so fall back to the user record id.
            if (userUid is null && recordUid is not null &&
                string.Equals(action, "Login", StringComparison.OrdinalIgnoreCase))
            {
                userUid = recordUid;
            }

            var entry = new AuditLog
            {
                LogDate = DateTime.Now,
                UserUid = userUid,
                ModuleName = entityName,
                ActionName = action,
                RecordUid = recordUid,
                Description = details,
                Ipaddress = http?.Connection.RemoteIpAddress?.ToString()
            };

            _db.AuditLogs.Add(entry);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write audit log for {Action} on {EntityName}.", action, entityName);
        }
    }
}
