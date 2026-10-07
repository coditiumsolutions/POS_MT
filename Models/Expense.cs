using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("BranchUid", Name = "IX_Expenses_BranchUID")]
[Index("ExpenseCategoryUid", Name = "IX_Expenses_ExpenseCategoryUID")]
[Index("ExpenseDate", Name = "IX_Expenses_ExpenseDate")]
[Index("ExpenseNo", Name = "IX_Expenses_ExpenseNo")]
public partial class Expense
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string ExpenseNo { get; set; } = null!;

    [Precision(0)]
    public DateTime ExpenseDate { get; set; }

    [Column("BranchUID")]
    public int? BranchUid { get; set; }

    [Column("ExpenseCategoryUID")]
    public int ExpenseCategoryUid { get; set; }

    [StringLength(500)]
    public string? Description { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [Column("PaymentMethodUID")]
    public int? PaymentMethodUid { get; set; }

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
