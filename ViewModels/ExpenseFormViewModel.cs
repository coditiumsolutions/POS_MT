using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class ExpenseFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Expense No")] public string ExpenseNo { get; set; } = string.Empty;
  [Display(Name="Expense Date")] public DateTime ExpenseDate { get; set; } = DateTime.Now;
  [Display(Name="Branch")] public int? BranchUid { get; set; }
  [Required, Display(Name="Category")] public int ExpenseCategoryUid { get; set; }
  [StringLength(500)] public string? Description { get; set; }
  public decimal Amount { get; set; }
  [Display(Name="Payment Method")] public int? PaymentMethodUid { get; set; }
  [StringLength(100), Display(Name="Reference No")] public string? ReferenceNo { get; set; }
  [StringLength(500)] public string? Remarks { get; set; }
}
