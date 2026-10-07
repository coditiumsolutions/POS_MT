using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchUid", Name = "IX_Terminals_BranchUID")]
[Index("TerminalCode", Name = "IX_Terminals_TerminalCode")]
public partial class Terminal
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string TerminalCode { get; set; } = null!;

    [StringLength(100)]
    public string TerminalName { get; set; } = null!;

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [StringLength(150)]
    public string? ComputerName { get; set; }

    [Column("IPAddress")]
    [StringLength(50)]
    public string? Ipaddress { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
