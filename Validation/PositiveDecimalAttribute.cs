using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PharmacySystem.Validation;

/// <summary>
/// Validates that a numeric decimal price or amount is non-negative (>= 0), or strictly positive.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class PositiveDecimalAttribute : ValidationAttribute, IClientModelValidator
{
    public bool AllowZero { get; set; } = true;

    public PositiveDecimalAttribute()
    {
        ErrorMessage = "PositivePriceRequired";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null)
        {
            return ValidationResult.Success;
        }

        if (value is decimal decValue)
        {
            if (AllowZero && decValue >= 0m)
            {
                return ValidationResult.Success;
            }
            if (!AllowZero && decValue > 0m)
            {
                return ValidationResult.Success;
            }
        }

        var memberNames = validationContext.MemberName != null
            ? new[] { validationContext.MemberName }
            : null;

        return new ValidationResult(FormatErrorMessage(validationContext.DisplayName), memberNames);
    }

    public void AddValidation(ClientModelValidationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        decimal min = AllowZero ? 0.00m : 0.01m;
        context.Attributes["data-val"] = "true";
        context.Attributes["data-val-range"] = FormatErrorMessage(context.ModelMetadata.GetDisplayName());
        context.Attributes["data-val-range-min"] = min.ToString(CultureInfo.InvariantCulture);
        context.Attributes["data-val-range-max"] = "999999999.99";
    }
}
