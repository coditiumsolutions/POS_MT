using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public sealed class EmployeeAttendanceMarkPageViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "Attendance date")]
    public DateTime AttendanceDate { get; set; } = DateTime.Today;

    [Display(Name = "Branch")]
    public int? BranchUid { get; set; }

    public List<SelectListItem> BranchOptions { get; set; } = [];

    public List<EmployeeAttendanceEmployeeRowViewModel> Employees { get; set; } = [];

    /// <summary>Employee UIDs already marked present for the selected calendar date (any branch).</summary>
    public HashSet<int> AlreadyPresentUids { get; set; } = [];

    /// <summary>EmployeeUid → branch name where they were already marked for this date.</summary>
    public Dictionary<int, string> AlreadyPresentBranchNames { get; set; } = [];
}

public sealed class EmployeeAttendanceEmployeeRowViewModel
{
    public int Uid { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? MobileNo1 { get; set; }
}

public sealed class EmployeeAttendanceMarkRequest
{
    [DataType(DataType.Date)]
    public DateTime AttendanceDate { get; set; } = DateTime.Today;

    public int BranchUid { get; set; }

    public int[]? SelectedEmployeeUids { get; set; }
}

public sealed class EmployeeAttendanceListPageViewModel
{
    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime FromDate { get; set; } = DateTime.Today.AddDays(-7);

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime ToDate { get; set; } = DateTime.Today;

    [Display(Name = "Branch")]
    public int? BranchUid { get; set; }

    public List<SelectListItem> BranchOptions { get; set; } = [];

    public List<EmployeeAttendanceListItemViewModel> Rows { get; set; } = [];
}

public sealed class EmployeeAttendanceListItemViewModel
{
    public int Uid { get; set; }
    public DateTime AttendanceDate { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public string Status { get; set; } = "Present";
}
