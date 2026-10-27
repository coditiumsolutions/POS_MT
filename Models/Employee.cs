using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index(nameof(EmployeeCode), Name = "IX_Employees_EmployeeCode")]
[Index(nameof(Cnic), Name = "IX_Employees_CNIC")]
[Index(nameof(EmployeeName), Name = "IX_Employees_EmployeeName")]
public partial class Employee
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(30)]
    public string EmployeeCode { get; set; } = string.Empty;

    [StringLength(200)]
    public string EmployeeName { get; set; } = string.Empty;

    [Column("CNIC")]
    [StringLength(20)]
    public string? Cnic { get; set; }

    [StringLength(30)]
    public string? MobileNo1 { get; set; }

    [StringLength(30)]
    public string? MobileNo2 { get; set; }

    [StringLength(500)]
    public string? PresentAddress { get; set; }

    [StringLength(500)]
    public string? PermanentAddress { get; set; }

    /// <summary>Base / total salary before allowances and deductions.</summary>
    [Column(TypeName = "decimal(18, 2)")]
    public decimal TotalSalary { get; set; }

    public bool IsActive { get; set; } = true;

    [Precision(0)]
    public DateTime CreatedDate { get; set; }

    [Precision(0)]
    public DateTime? UpdatedDate { get; set; }
}
