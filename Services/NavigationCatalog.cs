namespace POS_MT.Services;

public sealed record NavLinkItem(
    string Title,
    string? Controller = null,
    string Action = "Index",
    string? Filter = null,
    string IconClass = "bi-circle");

public sealed record TopNavArea(string Key, string Title, IReadOnlyList<NavLinkItem> SubLinks);

public sealed record ModuleCardItem(string Name, string IconClass, string? Controller = null, string Action = "Index");

public static class NavigationCatalog
{
    public const string SessionKey = "CurrentTopNav";

    /// <summary>Second top-navbar main links (All Modules expands via twistee).</summary>
    public static IReadOnlyList<TopNavArea> TopAreas { get; } =
    [
        new("POS", "POS",
        [
            new("Cash POS", "POS", "Cash", IconClass: "bi-cash-stack"),
            new("Monthly POS", "POS", "Credit", IconClass: "bi-calendar-month")
        ]),
        new("Inventories", "Inventories",
        [
            new("Add Inventory", "Inventories", "Create", IconClass: "bi-plus-square"),
            new("All Inventory", "Inventories", IconClass: "bi-boxes")
        ]),
        new("Customers", "Customers",
        [
            new("Add Customers", "Customers", "Create", IconClass: "bi-person-plus"),
            new("All Customers", "Customers", IconClass: "bi-people")
        ]),
        new("Reports", "Reports",
        [
            new("Sales Report", "Reports", "TotalSale", IconClass: "bi-graph-up-arrow")
        ]),
        new("AllModules", "All Modules",
        [
            new("Vendors", "Vendors", IconClass: "bi-truck"),
            new("Products", "Products", IconClass: "bi-box-seam"),
            new("Sales Invoice", "SalesInvoices", IconClass: "bi-receipt"),
            new("Report", "Reports", "TotalSale", IconClass: "bi-graph-up")
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
            new("Add Invoice", "SalesInvoices", "Create", IconClass: "bi-file-earmark-plus"),
            new("All Invoice", "SalesInvoices", IconClass: "bi-receipt"),
            new("Invoice Details", "SalesInvoiceDetails", IconClass: "bi-list-ul")
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

        return TopAreas.Concat(NestedModuleAreas).FirstOrDefault(a =>
            string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Title, key, StringComparison.OrdinalIgnoreCase));
    }
}
