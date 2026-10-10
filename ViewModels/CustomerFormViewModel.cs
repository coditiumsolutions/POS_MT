using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class CustomerFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(50), Display(Name = "Customer Code")]
    public string CustomerCode { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Customer Name")]
    public string CustomerName { get; set; } = string.Empty;

    [StringLength(30), Display(Name = "Mobile No")]
    public string? MobileNo { get; set; }

    [StringLength(30), Display(Name = "Phone No")]
    public string? PhoneNo { get; set; }

    [EmailAddress, StringLength(150)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Area { get; set; }

    [StringLength(50), Display(Name = "NTN")]
    public string? Ntn { get; set; }

    [Display(Name = "Opening Balance")]
    public decimal OpeningBalance { get; set; }

    [Display(Name = "Credit Limit")]
    public decimal CreditLimit { get; set; }

    [Display(Name = "Discount %")]
    public decimal DiscountPercent { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<SelectListItem> CityOptions { get; set; } = [];
    public IEnumerable<SelectListItem> AreaOptions { get; set; } = [];
}
