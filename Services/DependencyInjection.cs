using Microsoft.EntityFrameworkCore;
using POS_MT.Data;
using POS_MT.Interfaces;
using POS_MT.Options;

namespace POS_MT.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddPosServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StockMovementOptions>(configuration.GetSection(StockMovementOptions.SectionName));
        services.Configure<PosHubOptions>(configuration.GetSection(PosHubOptions.SectionName));

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
                "For offline use, run: POS_MT.exe offline pull (creates local SQLite).");
        }

        services.AddHttpContextAccessor();
        services.AddDistributedMemoryCache();
        services.AddSession(options =>
        {
            options.Cookie.Name = ".POS_MT.Session";
            options.Cookie.HttpOnly = true;
            options.IdleTimeout = TimeSpan.FromHours(8);
        });

        services.AddSingleton<IOfflineSqlitePaths, OfflineSqlitePaths>();
        services.AddSingleton<IPosConnectionRouter, PosConnectionRouter>();
        services.AddHostedService<PosConnectionRefreshService>();
        services.AddScoped<IOfflineSqliteSyncService, OfflineSqliteSyncService>();

        services.AddDbContext<POSDbContext>((sp, options) =>
        {
            var router = sp.GetRequiredService<IPosConnectionRouter>();
            var cs = router.GetOperationalConnectionString();
            if (router.IsSqliteActive)
            {
                options.UseSqlite(cs);
            }
            else
            {
                options.UseSqlServer(cs);
            }
        });

        services.AddSingleton<HubSyncStateStore>();
        services.AddScoped<IHubStatusService, HubStatusService>();
        services.AddScoped<ISyncOutboxService, SyncOutboxService>();
        services.AddHostedService<CentralSyncWorker>();

        services.AddScoped<IPasswordHashService, PasswordHashService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IStockMovementService, StockMovementService>();
        services.AddScoped<IInventoryLedgerService, InventoryLedgerService>();
        services.AddScoped<ICustomerAdvanceService, CustomerAdvanceService>();
        services.AddScoped<IEmployeeSchemaService, EmployeeSchemaService>();
        services.AddScoped<IOfflineSyncCatalogService, OfflineSyncCatalogService>();
        services.AddScoped<INavContextService, NavContextService>();

        return services;
    }
}
