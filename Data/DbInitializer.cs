using Microsoft.EntityFrameworkCore;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Data;

/// <summary>
/// Database Initializer.
/// Ensures the database and migrations are applied on startup.
/// Note: Hardcoded default users have been removed so the user can register their own administrator account directly.
/// </summary>
public static class DbInitializer
{
    public static async Task SeedDefaultUsersAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<PharmacyDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            // Ensure pending migrations are applied
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogWarning("Database migration check failed or skipped: {Message}", ex.Message);
        }
    }
}
