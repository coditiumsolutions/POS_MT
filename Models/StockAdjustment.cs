using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("AdjustmentDate", Name = "IX_StockAdjustments_AdjustmentDate")]
[Index("AdjustmentNo", Name = "IX_StockAdjustments_AdjustmentNo")]
[Index("WarehouseUid", Name = "IX_StockAdjustments_WarehouseUID")]
public partial class StockAdjustment
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string AdjustmentNo { get; set; } = null!;

    [Precision(0)]
    public DateTime AdjustmentDate { get; set; }

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [Column("WarehouseUID")]
    public int WarehouseUid { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [StringLength(50)]
    public string AdjustmentType { get; set; } = null!;

    [StringLength(500)]
    public string? Remarks { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = null!;

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
