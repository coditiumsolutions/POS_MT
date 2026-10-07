using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("VendorCode", Name = "IX_Vendors_VendorCode")]
[Index("VendorName", Name = "IX_Vendors_VendorName")]
public partial class Vendor
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string VendorCode { get; set; } = null!;

    [StringLength(200)]
    public string VendorName { get; set; } = null!;

    [StringLength(150)]
    public string? ContactPerson { get; set; }

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

    [Column("NTN")]
    [StringLength(50)]
    public string? Ntn { get; set; }

    [Column("STRN")]
    [StringLength(50)]
    public string? Strn { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal OpeningBalance { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal CreditLimit { get; set; }

    [StringLength(100)]
    public string? PaymentTerms { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
