using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("ProductUid", Name = "IX_InventoryTransactions_ProductUID")]
[Index("ReferenceUid", Name = "IX_InventoryTransactions_ReferenceUID")]
[Index("TransactionDate", Name = "IX_InventoryTransactions_TransactionDate")]
[Index("WarehouseUid", Name = "IX_InventoryTransactions_WarehouseUID")]
public partial class InventoryTransaction
{
    [Column("uid")]
    [Key]
    public long Uid { get; set; }

    [Precision(0)]
    public DateTime TransactionDate { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [Column("WarehouseUID")]
    public int WarehouseUid { get; set; }

    [StringLength(50)]
    public string TransactionType { get; set; } = null!;

    [Column("ReferenceUID")]
    public int? ReferenceUid { get; set; }

    [StringLength(50)]
    public string? ReferenceNo { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal QuantityIn { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal QuantityOut { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal BalanceQuantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitCost { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Column("TerminalUID")]
    public int? TerminalUid { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
