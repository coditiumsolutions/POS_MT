namespace POS_MT.Services;

public sealed record NavLinkItem(
    string Title,
    string? Controller = null,
    string Action = "Index",
    string? Filter = null,
    string IconClass = "bi-circle",
    int? ItemMonth = null,
    string? NavArea = null,
    IReadOnlyList<NavLinkItem>? Children = null);

public sealed record TopNavArea(string Key, string Title, IReadOnlyList<NavLinkItem> SubLinks);

public sealed record ModuleCardItem(string Name, string IconClass, string? Controller = null, string Action = "Index");

public sealed record ModuleSection(string Title, string? Subtitle, IReadOnlyList<ModuleCardItem> Modules);

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
            new("All Customers", "Customers", IconClass: "bi-people"),
            new("Add Payment", "CustomerPayments", "Create", IconClass: "bi-cash-coin"),
            new("All Payments", "CustomerPayments", IconClass: "bi-wallet2")
        ]),
        new("Reports", "Reports",
        [
            new("Sales Report", "Reports", "TotalSale", IconClass: "bi-graph-up-arrow"),
            new("Daily Report", "Reports", "DailyReport", IconClass: "bi-calendar-day"),
            new("Slow / Dead Stock", "StockMovement", IconClass: "bi-hourglass-split", NavArea: "Reports")
        ]),
        new("Employees", "Employees",
        [
            new("All Employees", "Employees", IconClass: "bi-people"),
            new("Add Employee", "Employees", "Create", IconClass: "bi-person-plus"),
            new("Salary Payments", "EmployeePayments", IconClass: "bi-cash-coin")
        ]),
        new("AllModules", "All Modules",
        [
            new("Vendors", "Vendors", IconClass: "bi-truck"),
            new("Products", "Products", IconClass: "bi-box-seam"),
            new("Inventories", "Inventories", IconClass: "bi-boxes"),
            new("Purchases", "PurchaseInvoices", IconClass: "bi-cart-plus"),
            new("Sales", "SalesInvoices", IconClass: "bi-receipt"),
            new("Expenses", "Expenses", IconClass: "bi-credit-card"),
            new("Employees", "Employees", IconClass: "bi-person-badge")
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
            new("Categories", IconClass: "bi-tags", Children:
            [
                new("Add Category", "ProductCategories", "Create", IconClass: "", NavArea: "Products"),
                new("Categories", "ProductCategories", IconClass: "", NavArea: "Products")
            ]),
            new("Products", IconClass: "bi-box-seam", Children:
            [
                new("Add Product", "Products", "Create", IconClass: ""),
                new("All Products", "Products", IconClass: "")
            ])
        ]),
        new("Purchases", "Purchases",
        [
            new("All Purchases", "PurchaseInvoices", IconClass: "bi-cart-plus"),
            new("Add Invoice", "PurchaseInvoices", "Create", IconClass: "bi-plus-circle")
        ]),
        new("SalesInvoice", "Sales Invoice",
        [
            new("All Sales", "SalesInvoices", IconClass: "bi-receipt"),
            new("All Sale Items", "SalesInvoiceDetails", IconClass: "bi-list-ul")
        ])
    ];

    public static IReadOnlyList<ModuleSection> MainModuleSections { get; } =
    [
        new("Products", "Catalog, categories, and units",
        [
            new("Products", "bi-box-seam", "Products"),
            new("Product Categories", "bi-tags", "ProductCategories"),
            new("Product Units", "bi-rulers", "ProductUnits")
        ]),
        new("Inventory", "Stock levels and adjustments",
        [
            new("Inventory", "bi-boxes", "Inventories"),
            new("Stock Adjustments", "bi-arrow-left-right", "StockAdjustments")
        ]),
        new("Sales & Purchases", "Trading partners, invoices, and payments",
        [
            new("Vendors", "bi-truck", "Vendors"),
            new("Customers", "bi-people", "Customers"),
            new("Purchases", "bi-cart-plus", "PurchaseInvoices"),
            new("Sales", "bi-receipt", "SalesInvoices"),
            new("Sales Returns", "bi-arrow-return-left", "SalesReturns"),
            new("Customer Payments", "bi-cash-coin", "CustomerPayments"),
            new("Vendor Payments", "bi-wallet2", "VendorPayments")
        ]),
        new("Expenses", "Business expense tracking",
        [
            new("Expense Categories", "bi-folder2", "ExpenseCategories"),
            new("Expenses", "bi-credit-card", "Expenses")
        ]),
        new("HR & Payroll", "Employees, salary, allowances, and payments",
        [
            new("Employees", "bi-person-badge", "Employees"),
            new("Salary Payments", "bi-cash-coin", "EmployeePayments")
        ]),
        new("Organization", "Users, terminals, branches, and warehouses",
        [
            new("Users", "bi-person-gear", "Users"),
            new("Terminals", "bi-display", "Terminals"),
            new("Branches", "bi-building", "Branches"),
            new("Warehouses", "bi-house-door", "Warehouses")
        ]),
        new("Reports & Logs", "Sales reports and audit history",
        [
            new("Reports", "bi-graph-up", "Reports", "TotalSale"),
            new("Audit Logs", "bi-journal-text", "AuditLogs")
        ])
    ];

    public static IReadOnlyList<ModuleCardItem> MainModules { get; } =
        MainModuleSections.SelectMany(s => s.Modules).ToList();

    public static IReadOnlyList<ModuleCardItem> SetupModules { get; } =
    [
        new("Users", "bi-person-gear", "Users"),
        new("Terminals", "bi-display", "Terminals"),
        new("Branches", "bi-building", "Branches"),
        new("Warehouses", "bi-house-door", "Warehouses"),
        new("Configs", "bi-sliders", "Configurations"),
        new("Sync Tables", "bi-arrow-left-right", "OfflineSyncTables"),
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
