using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("Email", Name = "IX_Users_Email")]
[Index("UserName", Name = "IX_Users_UserName")]
public partial class User
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(100)]
    public string UserName { get; set; } = null!;

    [StringLength(200)]
    public string FullName { get; set; } = null!;

    [StringLength(500)]
    public string PasswordHash { get; set; } = null!;

    [StringLength(30)]
    public string? MobileNo { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(50)]
    public string RoleName { get; set; } = null!;

    [StringLength(50)]
    public string? UserType { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime? LastLoginDate { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
