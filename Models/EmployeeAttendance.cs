using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace POS_MT.Models;

[Table("EmployeeAttendance")]
[Index(nameof(EmployeeUid), nameof(AttendanceDate), IsUnique = true,
    Name = "UX_EmployeeAttendance_Employee_Date")]
[Index(nameof(AttendanceDate), Name = "IX_EmployeeAttendance_AttendanceDate")]
[Index(nameof(BranchUid), Name = "IX_EmployeeAttendance_BranchUID")]
[Index(nameof(EmployeeUid), Name = "IX_EmployeeAttendance_EmployeeUID")]
public partial class EmployeeAttendance
{
    [Column("uid")]
    [Key]
    public int Uid { get; set; }

    [Column("EmployeeUID")]
    public int EmployeeUid { get; set; }

    [Column("BranchUID")]
    public int BranchUid { get; set; }

    /// <summary>Calendar day the employee was marked present.</summary>
    [Precision(0)]
    public DateTime AttendanceDate { get; set; }

    [StringLength(30)]
    public string Status { get; set; } = "Present";

    [Column("UserUID")]
    public int? UserUid { get; set; }

    [Precision(0)]
    public DateTime CreatedDate { get; set; }
}
