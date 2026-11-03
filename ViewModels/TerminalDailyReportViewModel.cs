using Microsoft.AspNetCore.Mvc.Rendering;

namespace POS_MT.ViewModels;

public class TerminalDailyReportViewModel
{
    public DateTime DateFrom { get; set; }
    public DateTime DateTo { get; set; }
    public string? Branch { get; set; }
    public string? Terminal { get; set; }
    public string? UserName { get; set; }
    public IEnumerable<SelectListItem> Branches { get; set; } = [];
    public IEnumerable<SelectListItem> Terminals { get; set; } = [];
    public IEnumerable<SelectListItem> Users { get; set; } = [];
    public int TotalInvoices { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
    public List<TerminalDailyReportRowViewModel> Rows { get; set; } = [];
}

public class TerminalDailyReportRowViewModel
{
    public string BranchName { get; set; } = "Unassigned";
    public string TerminalName { get; set; } = "Unassigned";
    public string UserName { get; set; } = "Unassigned";
    public int InvoiceCount { get; set; }
    public decimal TotalNetAmount { get; set; }
    public decimal TotalPaidAmount { get; set; }
}
