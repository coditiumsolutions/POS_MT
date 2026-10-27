using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Models;

namespace POS_MT.Services;

public sealed class AuthService : IAuthService
{
    private const int MaxFailedAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly POSDbContext _db;
    private readonly IPasswordHashService _passwordHashService;
    private readonly IAuditService _auditService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        POSDbContext db,
        IPasswordHashService passwordHashService,
        IAuditService auditService,
        IHttpContextAccessor httpContextAccessor,
        IMemoryCache cache,
        ILogger<AuthService> logger)
    {
        _db = db;
        _passwordHashService = passwordHashService;
        _auditService = auditService;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
        _logger = logger;
    }

    public async Task<AuthResult> SignInAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(password))
        {
            return AuthResult.Failed("Username and password are required.");
        }

        var normalizedUserName = userName.Trim();
        var lockKey = "login-lock:" + normalizedUserName.ToLowerInvariant();
        var failKey = "login-fail:" + normalizedUserName.ToLowerInvariant();

        if (_cache.TryGetValue(lockKey, out _))
        {
            return AuthResult.Failed("Account temporarily locked after too many failed attempts. Try again in 15 minutes.");
        }

        User? user;
        try
        {
            user = await _db.Users
                .FirstOrDefaultAsync(u => u.UserName == normalizedUserName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query Users for login.");
            return AuthResult.Failed("Unable to verify login at this time. Please try again.");
        }

        if (user is null || !user.IsActive || !_passwordHashService.Verify(user.PasswordHash, password))
        {
            var fails = _cache.GetOrCreate(failKey, e =>
            {
                e.AbsoluteExpirationRelativeToNow = LockoutDuration;
                return 0;
            });
            fails++;
            _cache.Set(failKey, fails, LockoutDuration);
            if (fails >= MaxFailedAttempts)
            {
                _cache.Set(lockKey, true, LockoutDuration);
                _cache.Remove(failKey);
            }

            await _auditService.WriteAsync(
                action: "Login",
                entityName: "Users",
                entityId: null,
                details: $"Failed login attempt for '{normalizedUserName}' ({fails}/{MaxFailedAttempts}).",
                cancellationToken);

            return AuthResult.Failed("Invalid username or password.");
        }

        _cache.Remove(failKey);
        _cache.Remove(lockKey);

        var now = DateTime.Now;
        user.LastLoginDate = now;
        user.UpdatedDate = now;

        if (_passwordHashService.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHashService.Hash(password);
        }

        _db.UserSessions.Add(new UserSession
        {
            UserUid = user.Uid,
            LoginDate = now,
            Ipaddress = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            SessionStatus = "Active"
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update login metadata for user {UserUid}.", user.Uid);
        }

        await _auditService.WriteAsync(
            action: "Login",
            entityName: "Users",
            entityId: user.Uid.ToString(),
            details: $"User '{user.UserName}' signed in.",
            cancellationToken);

        return AuthResult.Success(new SignedInUser
        {
            UserId = user.Uid.ToString(),
            UserName = user.UserName,
            DisplayName = string.IsNullOrWhiteSpace(user.FullName) ? user.UserName : user.FullName,
            Role = AppRoles.Normalize(user.RoleName),
            TerminalName = string.IsNullOrWhiteSpace(user.TerminalName) ? null : user.TerminalName.Trim(),
            Branch = string.IsNullOrWhiteSpace(user.Branch) ? null : user.Branch.Trim()
        });
    }
}
