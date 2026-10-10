using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("CustomerCode", Name = "IX_Customers_CustomerCode")]
[Index("CustomerName", Name = "IX_Customers_CustomerName")]
[Index("MobileNo", Name = "IX_Customers_MobileNo")]
public partial class Customer
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string CustomerCode { get; set; } = null!;

    [StringLength(200)]
    public string CustomerName { get; set; } = null!;

    [StringLength(30)]
    public string? MobileNo { get; set; }

    [StringLength(30)]
    public string? PhoneNo { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? City { get; set; }

    [StringLength(100)]
    public string? Area { get; set; }

    [Column("NTN")]
    [StringLength(50)]
    public string? Ntn { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal OpeningBalance { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal CreditLimit { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
