using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;

namespace POS_MT.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddPosServices(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("POS_MT");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'POS_MT' is missing from configuration.");
        }

        if (connectionString.Contains("YOUR_SQL_SERVER", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("YOUR_USER", StringComparison.OrdinalIgnoreCase)
            || connectionString.Contains("YOUR_PASSWORD", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Connection string 'POS_MT' still has placeholder values. " +
                "On the server, set ConnectionStrings:POS_MT in appsettings.json (or environment variables). " +
                "Locally, use appsettings.Development.json. Publish copies the Development connection string into the published appsettings.json.");
        }

        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = ".POS_MT.Session";
            options.Cookie.HttpOnly = true;
            options.IdleTimeout = TimeSpan.FromHours(8);
        });

        services.AddDbContext<POSDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<INavContextService, NavContextService>();

        return services;
    }
}
