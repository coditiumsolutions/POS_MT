using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchUid", Name = "IX_Warehouses_BranchUID")]
[Index("WarehouseCode", Name = "IX_Warehouses_WarehouseCode")]
public partial class Warehouse
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string WarehouseCode { get; set; } = null!;

    [StringLength(150)]
    public string WarehouseName { get; set; } = null!;

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    public string? MobileNo { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
