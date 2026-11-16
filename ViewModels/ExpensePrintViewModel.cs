namespace POS_MT.ViewModels;

public sealed class ExpensePrintViewModel
{
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public DateTime PrintedAt { get; set; } = DateTime.Now;
    public int ExpenseCount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<ExpensePrintRowViewModel> Rows { get; set; } = [];
}

public sealed class ExpensePrintRowViewModel
{
    public string ExpenseNo { get; set; } = string.Empty;
    public DateTime ExpenseDate { get; set; }
    public string? CategoryName { get; set; }
    public string? BranchName { get; set; }
    public string? Description { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal Amount { get; set; }
}
