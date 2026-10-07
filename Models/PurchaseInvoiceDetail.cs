using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("ProductUid", Name = "IX_PurchaseInvoiceDetails_ProductUID")]
[Index("PurchaseInvoiceUid", Name = "IX_PurchaseInvoiceDetails_PurchaseInvoiceUID")]
public partial class PurchaseInvoiceDetail
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("PurchaseInvoiceUID")]
    public int PurchaseInvoiceUid { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [StringLength(50)]
    public string? ProductCode { get; set; }

    [StringLength(200)]
    public string? ProductName { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitCost { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal DiscountPercent { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal DiscountAmount { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal TaxPercent { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal LineTotal { get; set; }

    [StringLength(100)]
    public string? BatchNo { get; set; }

    public DateOnly? ExpiryDate { get; set; }

    [StringLength(300)]
    public string? Remarks { get; set; }
}
