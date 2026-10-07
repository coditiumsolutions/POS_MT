using System.ComponentModel.DataAnnotations;

namespace POS_MT.ViewModels;

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Username is required.")]
    [StringLength(100)]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [DataType(DataType.Password)]
    [StringLength(200)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Remember me")]
    public bool RememberMe { get; set; }
}
