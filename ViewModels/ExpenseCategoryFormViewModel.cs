using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class ExpenseCategoryFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Category Code")] public string CategoryCode { get; set; } = string.Empty;
  [Required, StringLength(150), Display(Name="Category Name")] public string CategoryName { get; set; } = string.Empty;
  [StringLength(300)] public string? Description { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
