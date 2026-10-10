using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PharmacySystem.Validation;

/// <summary>
/// Validates that an integer quantity is strictly greater than zero.
/// Emits unobtrusive validation attributes for client-side enforcement.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class PositiveQuantityAttribute : ValidationAttribute, IClientModelValidator
{
    public bool AllowZero { get; set; } = false;

    public PositiveQuantityAttribute()
    {
        ErrorMessage = "PositiveQuantityRequired";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success; // [Required] handles nullability
        }

        if (value is int intValue)
        {
            if (AllowZero && intValue >= 0) return ValidationResult.Success;
            if (!AllowZero && intValue > 0) return ValidationResult.Success;
        }

        if (value is long longValue)
        {
            if (AllowZero && longValue >= 0) return ValidationResult.Success;
            if (!AllowZero && longValue > 0) return ValidationResult.Success;
        }

        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        int min = AllowZero ? 0 : 1;
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-range"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
        context.Attributes["data-val-range-min"] = min.ToString();
        context.Attributes["data-val-range-max"] = int.MaxValue.ToString();
    }
}
