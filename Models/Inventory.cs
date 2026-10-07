using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Table("Inventory")]
[Index("ProductUid", Name = "IX_Inventory_ProductUID")]
[Index("WarehouseUid", Name = "IX_Inventory_WarehouseUID")]
public partial class Inventory
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [Column("WarehouseUID")]
    public int WarehouseUid { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal QuantityIn { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal QuantityOut { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal CurrentQuantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal AverageCost { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LastPurchasePrice { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LastSalePrice { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal MinimumStock { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal MaximumStock { get; set; }

    [Precision(0)]
    public DateTime? LastStockDate { get; set; }

    [Precision(0)]
    public DateTime UpdatedDate { get; set; }
}
