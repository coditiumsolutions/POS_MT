using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchCode", Name = "IX_Branches_BranchCode")]
public partial class Branch
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string BranchCode { get; set; } = null!;

    [StringLength(150)]
    public string BranchName { get; set; } = null!;

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(150)]
    public string? ContactPerson { get; set; }

    [StringLength(30)]
    public string? MobileNo { get; set; }

    [StringLength(30)]
    public string? PhoneNo { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
