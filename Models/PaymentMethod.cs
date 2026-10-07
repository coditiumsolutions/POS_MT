using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("PaymentCode", Name = "IX_PaymentMethods_PaymentCode")]
public partial class PaymentMethod
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(30)]
    public string PaymentCode { get; set; } = null!;

    [StringLength(100)]
    public string PaymentName { get; set; } = null!;

    public bool IsCash { get; set; }

    public bool IsCredit { get; set; }

    public bool IsActive { get; set; }
}
