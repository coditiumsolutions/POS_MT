using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index(nameof(EmployeeUid), Name = "IX_EmployeeDeductions_EmployeeUID")]
public partial class EmployeeDeduction
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("EmployeeUID")]
    public int EmployeeUid { get; set; }

    [StringLength(150)]
    public string Title { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    public bool IsActive { get; set; } = true;

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
