using System.ComponentModel.DataAnnotations;
using PharmacySystem.Enums;
using PharmacySystem.Validation;

namespace PharmacySystem.Models.ViewModels.Expenses;

public class ExpenseListItemViewModel
{
    public int Id { get; set; }
    public DateTime ExpenseDate { get; set; }
    public ExpenseCategory Category { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public string RecordedByName { get; set; } = string.Empty;
}

public class CreateExpenseViewModel
{
    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "ExpenseDate")]
    public DateTime ExpenseDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Category")]
    public ExpenseCategory Category { get; set; } = ExpenseCategory.Electricity;

    [Required(ErrorMessage = "RequiredField")]
    [PositiveDecimal(AllowZero = false, ErrorMessage = "PositiveDecimal")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}

public class EditExpenseViewModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "ExpenseDate")]
    public DateTime ExpenseDate { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [Display(Name = "Category")]
    public ExpenseCategory Category { get; set; }

    [Required(ErrorMessage = "RequiredField")]
    [PositiveDecimal(AllowZero = false, ErrorMessage = "PositiveDecimal")]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [StringLength(500, ErrorMessage = "StringLengthMax")]
    [Display(Name = "Description")]
    public string? Description { get; set; }
}
