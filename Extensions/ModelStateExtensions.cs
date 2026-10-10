using Microsoft.AspNetCore.Mvc.ModelBinding;
using PharmacySystem.Exceptions;

namespace PharmacySystem.Extensions;

public static class ModelStateExtensions
{
    /// <summary>
    /// Converts domain exceptions into clean, user-friendly ModelState errors without technical details.
    /// </summary>
    public static void AddDomainException(this ModelStateDictionary modelState, Exception ex, string? propertyName = null)
    {
        string key = propertyName ?? string.Empty;

        if (ex is InsufficientStockException stockEx)
        {
            modelState.AddModelError(key, stockEx.Message);
        }
        else if (ex is InvalidBusinessOperationException bizEx)
        {
            modelState.AddModelError(key, bizEx.Message);
        }
        else
        {
            modelState.AddModelError(key, "An unexpected issue occurred while processing the operation.");
        }
    }
}
