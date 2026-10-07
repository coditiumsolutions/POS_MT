using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class WarehouseFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Warehouse Code")] public string WarehouseCode { get; set; } = string.Empty;
  [Required, StringLength(150), Display(Name="Warehouse Name")] public string WarehouseName { get; set; } = string.Empty;
  [Display(Name="Branch")] public int? BranchUid { get; set; }
  [StringLength(500)] public string? Address { get; set; }
  [StringLength(100)] public string? City { get; set; }
  [StringLength(150), Display(Name="Contact Person")] public string? ContactPerson { get; set; }
  [StringLength(30), Display(Name="Mobile No")] public string? MobileNo { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
