using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Auth;

public class RegisterAdminViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [EmailAddress(ErrorMessage = "InvalidEmail")]
    [StringLength(150, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "PasswordMinLength")]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
