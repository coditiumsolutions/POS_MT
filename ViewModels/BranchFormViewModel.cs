using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class BranchFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Branch Code")] public string BranchCode { get; set; } = string.Empty;
  [Required, StringLength(150), Display(Name="Branch Name")] public string BranchName { get; set; } = string.Empty;
  [StringLength(500)] public string? Address { get; set; }
  [StringLength(100)] public string? City { get; set; }
  [StringLength(150), Display(Name="Contact Person")] public string? ContactPerson { get; set; }
  [StringLength(30), Display(Name="Mobile No")] public string? MobileNo { get; set; }
  [StringLength(30), Display(Name="Phone No")] public string? PhoneNo { get; set; }
  [EmailAddress, StringLength(150)] public string? Email { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
