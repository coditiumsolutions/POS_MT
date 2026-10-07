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

    public static IReadOnlyList<TopNavArea> TopAreas { get; } =
    [
        new("POS", "POS",
        [
            new("Cash POS", "POS", "Cash", IconClass: "bi-cash-stack"),
            new("Credit POS", "POS", "Credit", IconClass: "bi-credit-card-2-front")
        ]),
        new("Vendors", "Vendors",
        [
            new("Add Vendor", "Vendors", "Create", IconClass: "bi-plus-circle"),
            new("All Vendor", "Vendors", IconClass: "bi-truck")
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
        new("Products", "Products",
        [
            new("Add Product", "Products", "Create", IconClass: "bi-plus-circle"),
            new("All Products", "Products", IconClass: "bi-box-seam")
        ]),
        new("SalesInvoice", "Sales Invoice",
        [
            new("Add Invoice", "SalesInvoices", "Create", IconClass: "bi-file-earmark-plus"),
            new("All Invoice", "SalesInvoices", IconClass: "bi-receipt")
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
        new("Reports", "bi-graph-up"),
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

    public static TopNavArea? FindArea(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        return TopAreas.FirstOrDefault(a =>
            string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(a.Title, key, StringComparison.OrdinalIgnoreCase));
    }
}
