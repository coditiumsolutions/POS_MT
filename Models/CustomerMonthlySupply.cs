using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

/// <summary>
/// Standing monthly supply template for a customer (default products/qty).
/// Day-specific changes live in <see cref="CustomerMonthlySupplyOverride"/>.
/// </summary>
[Table("CustomerMonthlySupply")]
[Index("CustomerCode", Name = "IX_CustomerMonthlySupply_CustomerCode")]
[Index("ProductCode", Name = "IX_CustomerMonthlySupply_ProductCode")]
[Index("CustomerUid", Name = "IX_CustomerMonthlySupply_CustomerUID")]
public partial class CustomerMonthlySupply
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string CustomerCode { get; set; } = null!;

    [StringLength(200)]
    public string CustomerName { get; set; } = null!;

    [StringLength(50)]
    public string ProductCode { get; set; } = null!;

    [StringLength(200)]
    public string ProductName { get; set; } = null!;

    [StringLength(100)]
    public string? BrandName { get; set; }

    [Column(TypeName = "decimal(18, 3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>Per-line delivery charge. Treat null as 0.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal? DeliveryCharges { get; set; }

    [Column("CustomerUID")]
    public int? CustomerUid { get; set; }

    [Column("ProductUID")]
    public int? ProductUid { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
