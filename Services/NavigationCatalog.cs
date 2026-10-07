namespace POS_MT.Services;

public sealed record NavLinkItem(string Title, string? Controller = null, string Action = "Index");

public sealed record TopNavArea(string Key, string Title, IReadOnlyList<NavLinkItem> SubLinks);

public sealed record ModuleCardItem(string Name, string IconClass, string? Controller = null, string Action = "Index");

public static class NavigationCatalog
{
    public const string SessionKey = "CurrentTopNav";

    public static IReadOnlyList<TopNavArea> TopAreas { get; } =
    [
        new("POS", "POS",
        [
            new("Cash Customers"),
            new("Credit Customers")
        ]),
        new("Vendors", "Vendors",
        [
            new("Add Vendor"),
            new("All Vendor")
        ]),
        new("Inventories", "Inventories",
        [
            new("Add Inventory"),
            new("All Inventory")
        ]),
        new("Customers", "Customers",
        [
            new("Credit Customers"),
            new("Add Customer")
        ]),
        new("Products", "Products",
        [
            new("Add Product"),
            new("All Products")
        ]),
        new("SalesInvoice", "Sales Invoice",
        [
            new("Add Invoice"),
            new("All Invoice")
        ])
    ];

    public static IReadOnlyList<ModuleCardItem> MainModules { get; } =
    [
        new("Products", "bi-box-seam"),
        new("Product Categories", "bi-tags"),
        new("Product Units", "bi-rulers"),
        new("Vendors", "bi-truck"),
        new("Customers", "bi-people"),
        new("Purchases", "bi-cart-plus"),
        new("Sales", "bi-receipt"),
        new("Customer Payments", "bi-cash-coin"),
        new("Vendor Payments", "bi-wallet2"),
        new("Expenses", "bi-credit-card"),
        new("Inventory", "bi-boxes"),
        new("Stock Adjustments", "bi-arrow-left-right"),
        new("Reports", "bi-graph-up"),
        new("Users", "bi-person-gear"),
        new("Terminals", "bi-display"),
        new("Branches", "bi-building"),
        new("Warehouses", "bi-house-door"),
        new("Audit Logs", "bi-journal-text")
    ];

    public static IReadOnlyList<ModuleCardItem> SetupModules { get; } =
    [
        new("Users", "bi-person-gear"),
        new("Terminals", "bi-display"),
        new("Branches", "bi-building"),
        new("Warehouses", "bi-house-door"),
        new("Audit Logs", "bi-journal-text")
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
