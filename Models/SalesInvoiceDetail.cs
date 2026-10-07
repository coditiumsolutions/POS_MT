using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("ProductUid", Name = "IX_SalesInvoiceDetails_ProductUID")]
[Index("SalesInvoiceUid", Name = "IX_SalesInvoiceDetails_SalesInvoiceUID")]
public partial class SalesInvoiceDetail
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("SalesInvoiceUID")]
    public int SalesInvoiceUid { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [StringLength(50)]
    public string? ProductCode { get; set; }

    [StringLength(200)]
    public string? ProductName { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

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

    [Column(TypeName = "decimal(18, 2)")]
    public decimal CostPrice { get; set; }

    [StringLength(300)]
    public string? Remarks { get; set; }
}
