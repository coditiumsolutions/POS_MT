using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class UserFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(100), Display(Name="Username")] public string UserName { get; set; } = string.Empty;
  [Required, StringLength(200), Display(Name="Full Name")] public string FullName { get; set; } = string.Empty;
  [DataType(DataType.Password), StringLength(200), Display(Name="Password")] public string? Password { get; set; }
  [StringLength(30), Display(Name="Mobile No")] public string? MobileNo { get; set; }
  [EmailAddress, StringLength(150)] public string? Email { get; set; }
  [Required, StringLength(50), Display(Name="Role")] public string RoleName { get; set; } = "Cashier";
  [StringLength(50), Display(Name="User Type")] public string? UserType { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
