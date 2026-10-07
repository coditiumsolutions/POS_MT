using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("CustomerUid", Name = "IX_CustomerPayments_CustomerUID")]
[Index("PaymentDate", Name = "IX_CustomerPayments_PaymentDate")]
[Index("PaymentNo", Name = "IX_CustomerPayments_PaymentNo")]
[Index("SalesInvoiceUid", Name = "IX_CustomerPayments_SalesInvoiceUID")]
public partial class CustomerPayment
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string PaymentNo { get; set; } = null!;

    [Precision(0)]
    public DateTime PaymentDate { get; set; }

    [Column("CustomerUID")]
    public int CustomerUid { get; set; }

    [Column("SalesInvoiceUID")]
    public int? SalesInvoiceUid { get; set; }

    [Column("PaymentMethodUID")]
    public int? PaymentMethodUid { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(100)]
    public string? ReferenceNo { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
