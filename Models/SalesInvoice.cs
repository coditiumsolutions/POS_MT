using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchUid", Name = "IX_SalesInvoices_BranchUID")]
[Index("CustomerUid", Name = "IX_SalesInvoices_CustomerUID")]
[Index("InvoiceDate", Name = "IX_SalesInvoices_InvoiceDate")]
[Index("InvoiceNo", Name = "IX_SalesInvoices_InvoiceNo")]
[Index("UserUid", Name = "IX_SalesInvoices_UserUID")]
public partial class SalesInvoice
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string InvoiceNo { get; set; } = null!;

    [Precision(0)]
    public DateTime InvoiceDate { get; set; }

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [Column("CustomerUID")]
    public int? CustomerUid { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [Column("PaymentMethodUID")]
    public int? PaymentMethodUid { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal SubTotal { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal OtherCharges { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal NetAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal BalanceAmount { get; set; }

    [StringLength(30)]
    public string PaymentStatus { get; set; } = null!;

    [StringLength(30)]
    public string InvoiceStatus { get; set; } = null!;

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
