using System.ComponentModel.DataAnnotations;
namespace POS_MT.ViewModels;
public sealed class VendorFormViewModel
{
    public int Uid { get; set; }
    [Required, StringLength(50), Display(Name = "Vendor Code")] public string VendorCode { get; set; } = string.Empty;
    [Required, StringLength(200), Display(Name = "Vendor Name")] public string VendorName { get; set; } = string.Empty;
    [StringLength(150), Display(Name = "Contact Person")] public string? ContactPerson { get; set; }
    [StringLength(30), Display(Name = "Mobile No")] public string? MobileNo { get; set; }
    [StringLength(30), Display(Name = "Phone No")] public string? PhoneNo { get; set; }
    [EmailAddress, StringLength(150)] public string? Email { get; set; }
    [StringLength(500)] public string? Address { get; set; }
    [StringLength(100)] public string? City { get; set; }
    [StringLength(50), Display(Name = "NTN")] public string? Ntn { get; set; }
    [StringLength(50), Display(Name = "STRN")] public string? Strn { get; set; }
    [Display(Name = "Opening Balance")] public decimal OpeningBalance { get; set; }
    [Display(Name = "Credit Limit")] public decimal CreditLimit { get; set; }
    [StringLength(100), Display(Name = "Payment Terms")] public string? PaymentTerms { get; set; }
    [Display(Name = "Active")] public bool IsActive { get; set; } = true;
}
