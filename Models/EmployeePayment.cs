using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Index(nameof(EmployeeUid), Name = "IX_EmployeePayments_EmployeeUID")]
[Index(nameof(PaymentDate), Name = "IX_EmployeePayments_PaymentDate")]
[Index(nameof(PaymentNo), Name = "IX_EmployeePayments_PaymentNo")]
public partial class EmployeePayment
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [StringLength(50)]
    public string PaymentNo { get; set; } = string.Empty;

    [Column("EmployeeUID")]
    public int EmployeeUid { get; set; }

    [Precision(0)]
    public DateTime PaymentDate { get; set; }

    /// <summary>Salary period month (1-12) this payment applies to.</summary>
    public int PeriodMonth { get; set; }

    /// <summary>Salary period year this payment applies to.</summary>
    public int PeriodYear { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
