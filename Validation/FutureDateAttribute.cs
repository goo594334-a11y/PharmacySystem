using System.ComponentModel.DataAnnotations;

namespace PharmacySystem.Validation;

/// <summary>
/// Validates that a date is today or in the future (e.g. for batch expiry date).
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class FutureDateAttribute : ValidationAttribute
{
    public FutureDateAttribute()
    {
        ErrorMessage = "FutureDateRequired";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        var todayDateOnly = DateOnly.FromDateTime(DateTime.Today);

        if (value is DateOnly dateOnlyValue)
        {
            if (dateOnlyValue >= todayDateOnly)
            {
                return ValidationResult.Success;
            }
        }
        else if (value is DateTime dateTimeValue)
        {
            if (dateTimeValue.Date >= DateTime.Today)
            {
                return ValidationResult.Success;
            }
        }

        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }
}
