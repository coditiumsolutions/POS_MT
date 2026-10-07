using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("CategoryCode", Name = "IX_ExpenseCategories_CategoryCode")]
public partial class ExpenseCategory
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string CategoryCode { get; set; } = null!;

    [StringLength(150)]
    public string CategoryName { get; set; } = null!;

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }
}
