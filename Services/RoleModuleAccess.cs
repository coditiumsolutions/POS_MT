namespace POS_MT.Services;

/// <summary>
/// Maps module/controller cards to the minimum role needed to open them.
/// </summary>
public static class RoleModuleAccess
{
    private static readonly Dictionary<string, string[]> ControllerRoles =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Users"] = [AppRoles.Administrator],
            ["Setup"] = [AppRoles.Administrator],
            ["Configurations"] = [AppRoles.Administrator],
            ["AuditLogs"] = [AppRoles.Administrator],
            ["OfflineSyncTables"] = [AppRoles.Administrator],
            ["Terminals"] = [AppRoles.Administrator],
            ["Branches"] = [AppRoles.Administrator],
            ["Warehouses"] = [AppRoles.Administrator],
            ["Products"] = [AppRoles.Administrator, AppRoles.Manager],
            ["ProductCategories"] = [AppRoles.Administrator, AppRoles.Manager],
            ["ProductUnits"] = [AppRoles.Administrator, AppRoles.Manager],
            ["Vendors"] = [AppRoles.Administrator, AppRoles.Manager],
            ["PurchaseInvoices"] = [AppRoles.Administrator, AppRoles.Manager],
            ["VendorPayments"] = [AppRoles.Administrator, AppRoles.Manager],
            ["Inventories"] = [AppRoles.Administrator, AppRoles.Manager],
            ["StockAdjustments"] = [AppRoles.Administrator, AppRoles.Manager],
            ["StockMovement"] = [AppRoles.Administrator, AppRoles.Manager],
            ["Expenses"] = [AppRoles.Administrator, AppRoles.Manager],
            ["ExpenseCategories"] = [AppRoles.Administrator, AppRoles.Manager],
            ["Employees"] = [AppRoles.Administrator, AppRoles.Manager],
            ["EmployeePayments"] = [AppRoles.Administrator, AppRoles.Manager],
            ["Reports"] = [AppRoles.Administrator, AppRoles.Manager],
            ["POS"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier],
            ["Customers"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier],
            ["CustomerPayments"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier],
            ["SalesInvoices"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier, AppRoles.Viewer],
            ["SalesReturns"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier],
            ["SalesInvoiceDetails"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier, AppRoles.Viewer],
            ["Dashboard"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier, AppRoles.Viewer],
            ["Modules"] = [AppRoles.Administrator, AppRoles.Manager, AppRoles.Cashier],
        };

    public static bool CanAccess(string? controller, System.Security.Claims.ClaimsPrincipal user)
    {
        if (string.IsNullOrWhiteSpace(controller))
        {
            return false;
        }

        if (!ControllerRoles.TryGetValue(controller, out var roles))
        {
            // Unknown module: Admin/Manager only.
            return user.IsInRole(AppRoles.Administrator) || user.IsInRole(AppRoles.Manager);
        }

        return roles.Any(user.IsInRole);
    }

    public static IEnumerable<ModuleCardItem> Filter(IEnumerable<ModuleCardItem> modules, System.Security.Claims.ClaimsPrincipal user) =>
        modules.Where(m => CanAccess(m.Controller, user));

    public static IEnumerable<ModuleSection> FilterSections(IEnumerable<ModuleSection> sections, System.Security.Claims.ClaimsPrincipal user) =>
        sections
            .Select(s => new ModuleSection(s.Title, s.Subtitle, Filter(s.Modules, user).ToList()))
            .Where(s => s.Modules.Count > 0);
}
