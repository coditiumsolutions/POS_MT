using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class EmployeeListItemViewModel
{
    public int Uid { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? Cnic { get; set; }
    public string? MobileNo1 { get; set; }
    public decimal TotalSalary { get; set; }
    public decimal NetSalary { get; set; }
    public decimal PaidThisMonth { get; set; }
    public decimal RemainingThisMonth { get; set; }
    public bool IsActive { get; set; }
}

public sealed class EmployeeFormViewModel
{
    public int Uid { get; set; }

    [Required, StringLength(30), Display(Name = "Employee Code")]
    public string EmployeeCode { get; set; } = string.Empty;

    [Required, StringLength(200), Display(Name = "Employee Name")]
    public string EmployeeName { get; set; } = string.Empty;

    [StringLength(20), Display(Name = "CNIC")]
    public string? Cnic { get; set; }

    [StringLength(30), Display(Name = "Mobile No 1")]
    public string? MobileNo1 { get; set; }

    [StringLength(30), Display(Name = "Mobile No 2")]
    public string? MobileNo2 { get; set; }

    [StringLength(500), Display(Name = "Present Address")]
    public string? PresentAddress { get; set; }

    [StringLength(500), Display(Name = "Permanent Address")]
    public string? PermanentAddress { get; set; }

    [Range(0, 999999999), Display(Name = "Total Salary")]
    public decimal TotalSalary { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeSalaryLineViewModel
{
    public int Uid { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsActive { get; set; }
}

public sealed class EmployeePaymentRowViewModel
{
    public int Uid { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class EmployeeDetailsViewModel
{
    public EmployeeFormViewModel Employee { get; set; } = new();
    public List<EmployeeSalaryLineViewModel> Allowances { get; set; } = [];
    public List<EmployeeSalaryLineViewModel> Deductions { get; set; } = [];
    public List<EmployeePaymentRowViewModel> Payments { get; set; } = [];

    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }

    public decimal TotalSalary { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetPayable { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal RemainingAmount { get; set; }

    [Required, StringLength(150)]
    public string NewAllowanceTitle { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal NewAllowanceAmount { get; set; }

    [Required, StringLength(150)]
    public string NewDeductionTitle { get; set; } = string.Empty;

    [Range(0.01, 999999999)]
    public decimal NewDeductionAmount { get; set; }

    [Range(0.01, 999999999)]
    public decimal NewPaymentAmount { get; set; }

    public DateTime NewPaymentDate { get; set; } = DateTime.Today;

    [StringLength(500)]
    public string? NewPaymentRemarks { get; set; }
}

public sealed class EmployeePaymentListItemViewModel
{
    public int Uid { get; set; }
    public string PaymentNo { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public int PeriodMonth { get; set; }
    public int PeriodYear { get; set; }
    public decimal Amount { get; set; }
    public string? Remarks { get; set; }
}

public sealed class EmployeePaymentCreateViewModel
{
    [Required]
    public int EmployeeUid { get; set; }

    [Required]
    public DateTime PaymentDate { get; set; } = DateTime.Today;

    [Range(1, 12)]
    public int PeriodMonth { get; set; } = DateTime.Today.Month;

    [Range(2000, 2100)]
    public int PeriodYear { get; set; } = DateTime.Today.Year;

    [Range(0.01, 999999999)]
    public decimal Amount { get; set; }

    [StringLength(500)]
    public string? Remarks { get; set; }
}
