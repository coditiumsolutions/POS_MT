using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class MonthlySupplyCustomerRowViewModel
{
    public int Uid { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string? MobileNo { get; set; }
    public string? PhoneNo { get; set; }
    public string? Area { get; set; }
}

public sealed class MonthlySupplyPageViewModel
{
    public List<MonthlySupplyCustomerRowViewModel> Customers { get; set; } = [];
    public int? OpenCustomerUid { get; set; }
    public string? Area { get; set; }
    public IEnumerable<SelectListItem> AreaOptions { get; set; } = [];
}

public sealed class MonthlySupplyItemsPageViewModel
{
    public List<MonthlySupplyCustomerRowViewModel> SelectedCustomers { get; set; } = [];
    public int ActiveCustomerUid { get; set; }
    public string ActiveCustomerCode { get; set; } = string.Empty;
    public string ActiveCustomerName { get; set; } = string.Empty;
    public string CustomerUidsQuery { get; set; } = string.Empty;
    public IEnumerable<SelectListItem> Products { get; set; } = [];
    public List<MonthlySupplyItemRowViewModel> Items { get; set; } = [];

    /// <summary>When set to Customers, Back returns to Customers Index.</summary>
    public string? ReturnTo { get; set; }
}

public sealed class DeliveryHandoverPageViewModel
{
    public string ShopName { get; set; } = "Shop";
    public DateTime SupplyDate { get; set; }
    public DateTime PrintedAt { get; set; }
    public bool SelectedOnly { get; set; }
    public string? AreaFilter { get; set; }
    public List<DeliveryHandoverRowViewModel> Customers { get; set; } = [];

    public IEnumerable<IGrouping<string, DeliveryHandoverRowViewModel>> CustomersByArea =>
        Customers
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Area) ? "(No Area)" : x.Area.Trim())
            .OrderBy(g => g.Key == "(No Area)")
            .ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase);
}

public sealed class DeliveryHandoverRowViewModel
{
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string Location { get; set; } = "—";
    public string? Phone { get; set; }
    public List<DeliveryHandoverItemViewModel> Items { get; set; } = [];
}

public sealed class DeliveryHandoverItemViewModel
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public sealed class MonthlySupplyItemRowViewModel
{
    public int Uid { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? BrandName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DeliveryCharges { get; set; }

    /// <summary>(Quantity × UnitPrice) + DeliveryCharges, rounded to whole number.</summary>
    public decimal ItemTotal => Math.Round((Quantity * UnitPrice) + DeliveryCharges, 0, MidpointRounding.AwayFromZero);
}

public sealed class MonthlySupplyUpdateItemRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public int CustomerUid { get; set; }

    [Range(0.01, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 99999999)]
    public decimal UnitPrice { get; set; }

    [Range(0, 99999999)]
    public decimal DeliveryCharges { get; set; }
}

public sealed class MonthlySupplyAddItemRequest
{
    [Required]
    public int CustomerUid { get; set; }

    [Required]
    public int ProductUid { get; set; }

    [Range(0.01, 999999)]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 99999999)]
    public decimal UnitPrice { get; set; }

    [Range(0, 99999999)]
    public decimal DeliveryCharges { get; set; }

    public string? CustomerUids { get; set; }

    public string? ReturnTo { get; set; }
}
