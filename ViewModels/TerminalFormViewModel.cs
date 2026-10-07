using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class TerminalFormViewModel {
  public int Uid { get; set; }
  [Required, StringLength(50), Display(Name="Terminal Code")] public string TerminalCode { get; set; } = string.Empty;
  [Required, StringLength(100), Display(Name="Terminal Name")] public string TerminalName { get; set; } = string.Empty;
  [Display(Name="Branch")] public int? BranchUid { get; set; }
  [StringLength(150), Display(Name="Computer Name")] public string? ComputerName { get; set; }
  [StringLength(50), Display(Name="IP Address")] public string? Ipaddress { get; set; }
  [Display(Name="Active")] public bool IsActive { get; set; } = true;
}
