using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchUid", Name = "IX_PurchaseInvoices_BranchUID")]
[Index("PurchaseDate", Name = "IX_PurchaseInvoices_PurchaseDate")]
[Index("PurchaseInvoiceNo", Name = "IX_PurchaseInvoices_PurchaseInvoiceNo")]
[Index("VendorUid", Name = "IX_PurchaseInvoices_VendorUID")]
public partial class PurchaseInvoice
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string PurchaseInvoiceNo { get; set; } = null!;

    [StringLength(100)]
    public string? VendorInvoiceNo { get; set; }

    [Precision(0)]
    public DateTime PurchaseDate { get; set; }

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [Column("WarehouseUID")]
    public int? WarehouseUid { get; set; }

    [Column("VendorUID")]
    public int VendorUid { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

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
