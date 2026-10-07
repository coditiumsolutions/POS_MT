using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("PaymentDate", Name = "IX_VendorPayments_PaymentDate")]
[Index("PaymentNo", Name = "IX_VendorPayments_PaymentNo")]
[Index("PurchaseInvoiceUid", Name = "IX_VendorPayments_PurchaseInvoiceUID")]
[Index("VendorUid", Name = "IX_VendorPayments_VendorUID")]
public partial class VendorPayment
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string PaymentNo { get; set; } = null!;

    [Precision(0)]
    public DateTime PaymentDate { get; set; }

    [Column("VendorUID")]
    public int VendorUid { get; set; }

    [Column("PurchaseInvoiceUID")]
    public int? PurchaseInvoiceUid { get; set; }

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
