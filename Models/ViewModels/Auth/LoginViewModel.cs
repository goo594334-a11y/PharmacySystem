using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Auth;

public class LoginViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [EmailAddress(ErrorMessage = "InvalidEmail")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "RememberMe")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
