using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class ProductUnitFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(20), Display(Name="Unit Code")] public string UnitCode { get; set; } = string.Empty;
  [Required, StringLength(50), Display(Name="Unit Name")] public string UnitName { get; set; } = string.Empty;
  [Display(Name="Decimal Allowed")] public bool DecimalAllowed { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
