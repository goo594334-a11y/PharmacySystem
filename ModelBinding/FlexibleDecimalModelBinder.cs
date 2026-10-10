using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PharmacySystem.ModelBinding;

/// <summary>
/// Model binder that flexibly parses decimal numbers across cultures,
/// supporting both dot (.), comma (,), and Arabic decimal separator (٫).
/// Prevents HTML5 input serialization and locale mismatches from failing validation.
/// </summary>
public class FlexibleDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        ArgumentNullException.ThrowIfNull(bindingContext);

        var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueProviderResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

        var rawValue = valueProviderResult.FirstValue;
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            if (bindingContext.ModelType == typeof(decimal))
            {
                // Non-nullable decimal without value is handled by Required attribute or default
                bindingContext.Result = ModelBindingResult.Success(0m);
            }
            return Task.CompletedTask;
        }

        // Normalize decimal separators (Arabic ٫, comma, and dot)
        var normalized = rawValue.Trim()
            .Replace("٫", ".")
            .Replace(",", ".");

        if (decimal.TryParse(normalized, NumberStyles.Number | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsedValue))
        {
            bindingContext.Result = ModelBindingResult.Success(parsedValue);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(
                bindingContext.ModelName,
                $"The value '{rawValue}' is not valid for {bindingContext.ModelMetadata.GetDisplayName()}.");
        }

        return Task.CompletedTask;
    }
}

public class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
        {
            return new FlexibleDecimalModelBinder();
        }

        return null;
    }
}
