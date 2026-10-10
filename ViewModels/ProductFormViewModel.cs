using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace POS_MT.ViewModels;

public sealed class ProductFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(50), Display(Name = "Product Code")]
    public string ProductCode { get; set; } = string.Empty;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [Required, StringLength(200), Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;

    [Display(Name = "Category")]
    public int? CategoryUid { get; set; }

    [Display(Name = "Unit")]
    public int? UnitUid { get; set; }

    [StringLength(100), Display(Name = "Brand")]
    public string? BrandName { get; set; }

    [Display(Name = "Purchase Price")]
    public decimal PurchasePrice { get; set; }

    [Display(Name = "Sale Price")]
    public decimal SalePrice { get; set; }

    [Display(Name = "Wholesale Price")]
    public decimal WholesalePrice { get; set; }

    [Display(Name = "Min Stock")]
    public decimal MinimumStock { get; set; }

    [Display(Name = "Max Stock")]
    public decimal MaximumStock { get; set; }

    [Display(Name = "Tax %")]
    public decimal TaxPercent { get; set; }

    [Display(Name = "Discount %")]
    public decimal DiscountPercent { get; set; }

    [Display(Name = "Taxable")]
    public bool IsTaxable { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500), Display(Name = "Image Path")]
    public string? ImagePath { get; set; }

    [Display(Name = "Product Image")]
    public IFormFile? ImageFile { get; set; }
}
