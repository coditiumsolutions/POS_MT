using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("Barcode", Name = "IX_Products_Barcode")]
[Index("CategoryUid", Name = "IX_Products_CategoryUID")]
[Index("ProductCode", Name = "IX_Products_ProductCode")]
[Index("ProductName", Name = "IX_Products_ProductName")]
public partial class Product
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string ProductCode { get; set; } = null!;

    [StringLength(100)]
    public string? Barcode { get; set; }

    [StringLength(200)]
    public string ProductName { get; set; } = null!;

    [Column("CategoryUID")]
    public int? CategoryUid { get; set; }

    [Column("UnitUID")]
    public int? UnitUid { get; set; }

    [StringLength(100)]
    public string? BrandName { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal PurchasePrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SalePrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal WholesalePrice { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal MinimumStock { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal MaximumStock { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal TaxPercent { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    public bool IsTaxable { get; set; }

    public bool IsActive { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? ImagePath { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
