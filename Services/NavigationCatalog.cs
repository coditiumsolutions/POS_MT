namespace POS_MT.Services;

public sealed record NavLinkItem(
    string Title,
    string? Controller = null,
    string Action = "Index",
    string? Filter = null,
    string IconClass = "bi-circle",
    int? ItemMonth = null,
    string? NavArea = null);

public sealed record TopNavArea(string Key, string Title, IReadOnlyList<NavLinkItem> SubLinks);

public sealed record ModuleCardItem(string Name, string IconClass, string? Controller = null, string Action = "Index");

public static class NavigationCatalog
{
    public const string SessionKey = "CurrentTopNav";

    /// <summary>Sidebar links when viewing Dashboard / Home charts.</summary>
    public static TopNavArea DashboardArea { get; } = new("Dashboard", "Dashboard",
    [
        new("Bar Graphs", "Dashboard", "Index", IconClass: "bi-bar-chart-fill", ItemMonth: 6),
        new("Pie Graphs", "Dashboard", "PieGraphs", IconClass: "bi-pie-chart-fill"),
        new("Slow / Dead Stock", "StockMovement", IconClass: "bi-hourglass-split", NavArea: "Dashboard")
    ]);

    /// <summary>Second top-navbar main links (All Modules expands via twistee).</summary>
    public static IReadOnlyList<TopNavArea> TopAreas { get; } =
    [
        new("POS", "POS",
        [
            new("Cash POS", "POS", "Cash", IconClass: "bi-cash-stack"),
            new("Credit POS", "POS", "Credit", IconClass: "bi-calendar-month"),
            new("Monthly Supply", "POS", "MonthlySupply", IconClass: "bi-calendar2-check")
        ]),
        new("Customers", "Customers",
        [
            new("Add Customers", "Customers", "Create", IconClass: "bi-person-plus"),
            new("All Customers", "Customers", IconClass: "bi-people")
        ]),
        new("SalesInvoice", "Invoices",
        [
            new("All Sales", "SalesInvoices", IconClass: "bi-receipt"),
            new("All Sale Items", "SalesInvoiceDetails", IconClass: "bi-list-ul")
        ]),
        new("Reports", "Reports",
        [
            new("Sales Report", "Reports", "TotalSale", IconClass: "bi-graph-up-arrow"),
            new("Slow / Dead Stock", "StockMovement", IconClass: "bi-hourglass-split", NavArea: "Reports")
        ]),
        new("AllModules", "All Modules",
        [
            new("Vendors", "Vendors", IconClass: "bi-truck"),
            new("Products", "Products", IconClass: "bi-box-seam"),
            new("Inventories", "Inventories", IconClass: "bi-boxes")
        ])
    ];

    /// <summary>Sidebar menus when a module under All Modules is opened.</summary>
    public static IReadOnlyList<TopNavArea> NestedModuleAreas { get; } =
    [
        new("Vendors", "Vendors",
        [
            new("Add Vendor", "Vendors", "Create", IconClass: "bi-plus-circle"),
            new("All Vendor", "Vendors", IconClass: "bi-truck")
        ]),
        new("Products", "Products",
        [
            new("Add Product", "Products", "Create", IconClass: "bi-plus-circle"),
            new("All Products", "Products", IconClass: "bi-box-seam")
        ]),
        new("SalesInvoice", "Sales Invoice",
        [
            new("All Sales", "SalesInvoices", IconClass: "bi-receipt"),
            new("All Sale Items", "SalesInvoiceDetails", IconClass: "bi-list-ul")
        ])
    ];

    public static IReadOnlyList<ModuleCardItem> MainModules { get; } =
    [
        new("Products", "bi-box-seam", "Products"),
        new("Product Categories", "bi-tags", "ProductCategories"),
        new("Product Units", "bi-rulers", "ProductUnits"),
        new("Vendors", "bi-truck", "Vendors"),
        new("Customers", "bi-people", "Customers"),
        new("Purchases", "bi-cart-plus", "PurchaseInvoices"),
        new("Sales", "bi-receipt", "SalesInvoices"),
        new("Customer Payments", "bi-cash-coin", "CustomerPayments"),
        new("Vendor Payments", "bi-wallet2", "VendorPayments"),
        new("Expense Categories", "bi-folder2", "ExpenseCategories"),
        new("Expenses", "bi-credit-card", "Expenses"),
        new("Inventory", "bi-boxes", "Inventories"),
        new("Stock Adjustments", "bi-arrow-left-right", "StockAdjustments"),
        new("Reports", "bi-graph-up", "Reports", "TotalSale"),
        new("Users", "bi-person-gear", "Users"),
        new("Terminals", "bi-display", "Terminals"),
        new("Branches", "bi-building", "Branches"),
        new("Warehouses", "bi-house-door", "Warehouses"),
        new("Audit Logs", "bi-journal-text", "AuditLogs")
    ];

    public static IReadOnlyList<ModuleCardItem> SetupModules { get; } =
    [
        new("Users", "bi-person-gear", "Users"),
        new("Terminals", "bi-display", "Terminals"),
        new("Branches", "bi-building", "Branches"),
        new("Warehouses", "bi-house-door", "Warehouses"),
        new("Configs", "bi-sliders", "Configurations"),
        new("Audit Logs", "bi-journal-text", "AuditLogs")
    ];

    public static bool IsUnderAllModules(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return string.Equals(key, "AllModules", StringComparison.OrdinalIgnoreCase)
               || NestedModuleAreas.Any(a => string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    public static TopNavArea? FindArea(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        if (string.Equals(key, DashboardArea.Key, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, DashboardArea.Title, StringComparison.OrdinalIgnoreCase))
        {
            return DashboardArea;
        }

        return TopAreas.Concat(NestedModuleAreas).FirstOrDefault(a =>
            string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Title, key, StringComparison.OrdinalIgnoreCase));
    }
}
