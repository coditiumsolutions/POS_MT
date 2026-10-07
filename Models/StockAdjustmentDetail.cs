using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("ProductUid", Name = "IX_StockAdjustmentDetails_ProductUID")]
[Index("StockAdjustmentUid", Name = "IX_StockAdjustmentDetails_StockAdjustmentUID")]
public partial class StockAdjustmentDetail
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("StockAdjustmentUID")]
    public int StockAdjustmentUid { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal SystemQuantity { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal PhysicalQuantity { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal AdjustmentQuantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitCost { get; set; }

    [StringLength(300)]
    public string? Remarks { get; set; }
}
