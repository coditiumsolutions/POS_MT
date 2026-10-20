using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

/// <summary>
/// Per-day override of the standing <see cref="CustomerMonthlySupply"/> template.
/// Quantity 0 = skip that product on SupplyDate. Products not in the template are one-off lines for that day.
/// </summary>
[Table("CustomerMonthlySupplyOverrides")]
[Index("CustomerUid", "ProductUid", "SupplyDate", IsUnique = true, Name = "UX_CustomerMonthlySupplyOverrides_Customer_Product_Date")]
[Index("SupplyDate", Name = "IX_CustomerMonthlySupplyOverrides_SupplyDate")]
[Index("CustomerUid", Name = "IX_CustomerMonthlySupplyOverrides_CustomerUID")]
public partial class CustomerMonthlySupplyOverride
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("CustomerUID")]
    public int CustomerUid { get; set; }

    [Column("ProductUID")]
    public int ProductUid { get; set; }

    [StringLength(50)]
    public string CustomerCode { get; set; } = null!;

    [StringLength(50)]
    public string ProductCode { get; set; } = null!;

    [StringLength(200)]
    public string ProductName { get; set; } = null!;

    [StringLength(100)]
    public string? BrandName { get; set; }

    [Column(TypeName = "date")]
    public DateTime SupplyDate { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
