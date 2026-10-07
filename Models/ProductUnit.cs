using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("UnitCode", Name = "IX_ProductUnits_UnitCode")]
public partial class ProductUnit
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(20)]
    public string UnitCode { get; set; } = null!;

    [StringLength(50)]
    public string UnitName { get; set; } = null!;

    public bool DecimalAllowed { get; set; }

    public bool IsActive { get; set; }
}
