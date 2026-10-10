using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Models.ViewModels.Auth;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [EmailAddress(ErrorMessage = "InvalidEmail")]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [EmailAddress(ErrorMessage = "InvalidEmail")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "PasswordMinLength")]
    [Display(Name = "NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [Display(Name = "CurrentPassword")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "PasswordMinLength")]
    [Display(Name = "NewPassword")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
    [Display(Name = "ConfirmPassword")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
