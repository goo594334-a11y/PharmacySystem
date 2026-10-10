using System.ComponentModel.DataAnnotations;
using PharmacySystem.Enums;

namespace PharmacySystem.Models.ViewModels.Users;

public class UserListItemViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public int SalesCount { get; set; }
    public int PurchasesCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateUserViewModel
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
    [Display(Name = "Role")]
    public UserRole Role { get; set; } = UserRole.Pharmacist;

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

    [Display(Name = "IsActive")]
    public bool IsActive { get; set; } = true;
}

public class EditUserViewModel
{
    public int Id { get; set; }

    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [StringLength(100, ErrorMessage = "StringLengthMax")]
    [Display(Name = "FullName")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Role")]
    public UserRole Role { get; set; }

    [Display(Name = "Status")]
    public bool IsActive { get; set; }
}

public class UserDetailsViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int TotalSalesCount { get; set; }
    public decimal TotalSalesAmount { get; set; }
    public int TotalPurchasesCount { get; set; }
    public int TotalAuditLogsCount { get; set; }
}

public class ResetUserPasswordViewModel
{
    public int UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}
