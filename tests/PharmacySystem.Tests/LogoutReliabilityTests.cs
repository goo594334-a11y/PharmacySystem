using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;
using Xunit;

namespace PharmacySystem.Tests;

public class LogoutReliabilityTests
{
    private class FakeAuthenticationService : IAuthenticationService
    {
        public bool SignOutCalled { get; private set; }
        public string? SignedOutScheme { get; private set; }

        public Task SignOutAsync(HttpContext context, string? scheme, AuthenticationProperties? properties)
        {
            SignOutCalled = true;
            SignedOutScheme = scheme;
            return Task.CompletedTask;
        }

        public Task<AuthenticateResult> AuthenticateAsync(HttpContext context, string? scheme) 
            => Task.FromResult(AuthenticateResult.NoResult());
        public Task ChallengeAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) 
            => Task.CompletedTask;
        public Task ForbidAsync(HttpContext context, string? scheme, AuthenticationProperties? properties) 
            => Task.CompletedTask;
        public Task SignInAsync(HttpContext context, string? scheme, ClaimsPrincipal principal, AuthenticationProperties? properties) 
            => Task.CompletedTask;
    }

    private class FakePasswordHasher : IPasswordHasherService
    {
        public string HashPassword(User user, string password) => password;
        public bool VerifyPassword(User user, string hashedPassword, string providedPassword) => hashedPassword == providedPassword;
    }

    private (PharmacyDbContext Context, FakeAuthenticationService AuthMock, DefaultHttpContext HttpContext) SetupTestEnvironment(int userId, int pharmacyId = 1)
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var context = new PharmacyDbContext(options);

        var authMock = new FakeAuthenticationService();
        var services = new ServiceCollection();
        services.AddSingleton<IAuthenticationService>(authMock);
        var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            User = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("PharmacyId", pharmacyId.ToString())
            }, CookieAuthenticationDefaults.AuthenticationScheme))
        };

        return (context, authMock, httpContext);
    }

    [Fact]
    public async Task NormalValidUser_Logout_RecordsAuditLog_AndInvalidatesCookie()
    {
        // Arrange: Valid pharmacist user
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 100);
        context.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true });
        context.Users.Add(new User { Id = 100, FullName = "Pharmacist", Email = "ph@test.com", Role = UserRole.Pharmacist, PharmacyId = 1, IsActive = true });
        await context.SaveChangesAsync();

        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Act
        await authService.SignOutAsync(httpContext);

        // Assert
        Assert.True(authMock.SignOutCalled, "Authentication cookie must be invalidated.");
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, authMock.SignedOutScheme);

        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Logout");
        Assert.NotNull(auditLog);
        Assert.Equal(100, auditLog.UserId);
        Assert.Equal("100", auditLog.EntityId);
        Assert.Equal(nameof(User), auditLog.EntityName);
    }

    [Fact]
    public async Task DeveloperUser_Logout_RecordsAuditLog_AndInvalidatesCookie()
    {
        // Arrange: Retained Developer user (Id 6)
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 6);
        context.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true });
        context.Users.Add(new User { Id = 6, FullName = "Yousef Ali", Email = "goo594334@gmail.com", Role = UserRole.Developer, PharmacyId = 1, IsActive = true });
        await context.SaveChangesAsync();

        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Act
        await authService.SignOutAsync(httpContext);

        // Assert
        Assert.True(authMock.SignOutCalled);
        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == 6);
        Assert.NotNull(auditLog);
        Assert.Equal("Logout", auditLog.Action);
    }

    [Fact]
    public async Task AdminUser_Logout_RecordsAuditLog_AndInvalidatesCookie()
    {
        // Arrange: Retained Admin user (Id 8)
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 8);
        context.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true });
        context.Users.Add(new User { Id = 8, FullName = "System Administrator", Email = "admin@pharmacy.local", Role = UserRole.Admin, PharmacyId = 1, IsActive = true });
        await context.SaveChangesAsync();

        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Act
        await authService.SignOutAsync(httpContext);

        // Assert
        Assert.True(authMock.SignOutCalled);
        var auditLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == 8);
        Assert.NotNull(auditLog);
        Assert.Equal("Logout", auditLog.Action);
    }

    [Fact]
    public async Task StaleDeletedUser_Logout_InvalidatesCookie_DoesNotInsertInvalidForeignKey_AndDoesNotThrow()
    {
        // Arrange: User 999 has been deleted from dbo.Users (only users 6 and 8 exist)
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 999);
        context.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true });
        context.Users.Add(new User { Id = 6, FullName = "Yousef Ali", Email = "goo594334@gmail.com", Role = UserRole.Developer, PharmacyId = 1, IsActive = true });
        context.Users.Add(new User { Id = 8, FullName = "System Administrator", Email = "admin@pharmacy.local", Role = UserRole.Admin, PharmacyId = 1, IsActive = true });
        await context.SaveChangesAsync();

        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Act: Must not throw SqlException / DbUpdateException
        await authService.SignOutAsync(httpContext);

        // Assert
        Assert.True(authMock.SignOutCalled, "Authentication cookie MUST be invalidated even for stale deleted identity.");

        // Confirm NO record with invalid UserId = 999 was inserted
        var invalidLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.UserId == 999);
        Assert.Null(invalidLog);

        // Confirm diagnostic audit log was recorded with UserId = null (NOT substituted with User 6 or 8)
        var staleLog = await context.AuditLogs.FirstOrDefaultAsync(a => a.Action == "LogoutStaleIdentity");
        Assert.NotNull(staleLog);
        Assert.Null(staleLog.UserId);
        Assert.Equal("999", staleLog.EntityId);
    }

    [Fact]
    public async Task StaleDeletedUser_DoesNotSubstitute_DeveloperOrAdminAsActor()
    {
        // Arrange: Stale user 11 was deleted
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 11);
        context.Pharmacies.Add(new Pharmacy { Id = 1, Name = "Main Pharmacy", IsActive = true });
        context.Users.Add(new User { Id = 6, FullName = "Developer", Email = "dev@system.local", Role = UserRole.Developer, PharmacyId = 1, IsActive = true });
        context.Users.Add(new User { Id = 8, FullName = "Admin", Email = "admin@system.local", Role = UserRole.Admin, PharmacyId = 1, IsActive = true });
        await context.SaveChangesAsync();

        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Act
        await authService.SignOutAsync(httpContext);

        // Assert
        var developerLogs = await context.AuditLogs.Where(a => a.UserId == 6).ToListAsync();
        var adminLogs = await context.AuditLogs.Where(a => a.UserId == 8).ToListAsync();

        Assert.Empty(developerLogs); // Must NOT substitute Developer Id 6!
        Assert.Empty(adminLogs);     // Must NOT substitute Admin Id 8!
    }

    [Fact]
    public async Task AuditLog_DatabaseFailure_DoesNotPrevent_SignOutAsync()
    {
        // Arrange: DbContext that has been disposed or throws on save
        var (context, authMock, httpContext) = SetupTestEnvironment(userId: 6);
        var authService = new AuthService(context, new FakePasswordHasher(), NullLogger<AuthService>.Instance);

        // Dispose context to simulate hard database connection drop or unexpected failure
        await context.DisposeAsync();

        // Act: Must not crash the caller
        var exception = await Record.ExceptionAsync(() => authService.SignOutAsync(httpContext));

        // Assert
        Assert.Null(exception); // Handled safely
        Assert.True(authMock.SignOutCalled, "SignOutAsync must still be invoked via finally block despite database error.");
    }
}
