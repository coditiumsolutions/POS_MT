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
    public decimal LineTotal => Quantity * UnitPrice;
}

public sealed class MonthlySupplyUpdateItemRequest
{
    [Required]
    public int Id { get; set; }

    [Required]
    public int CustomerUid { get; set; }

    [Range(0.1, 999999)]
    public decimal Quantity { get; set; }

    [Range(0, 99999999)]
    public decimal UnitPrice { get; set; }
}

public sealed class MonthlySupplyAddItemRequest
{
    [Required]
    public int CustomerUid { get; set; }

    [Required]
    public int ProductUid { get; set; }

    [Range(0.1, 999999)]
    public decimal Quantity { get; set; } = 1;

    [Range(0, 99999999)]
    public decimal UnitPrice { get; set; }

    public string? CustomerUids { get; set; }
}
