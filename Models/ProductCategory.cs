using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index("CategoryCode", Name = "IX_ProductCategories_CategoryCode")]
[Index("ParentCategoryUid", Name = "IX_ProductCategories_ParentCategoryUID")]
public partial class ProductCategory
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string CategoryCode { get; set; } = null!;

    [StringLength(150)]
    public string CategoryName { get; set; } = null!;

    [Column("ParentCategoryUID")]
    public int? ParentCategoryUid { get; set; }

    [StringLength(300)]
    public string? Description { get; set; }

    public bool IsActive { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
