using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Enums;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;
using Xunit;

namespace PharmacySystem.Tests;

public class DeveloperApprovalWorkflowTests
{
    private class FakePasswordHasher : IPasswordHasherService
    {
        public string HashPassword(User user, string password) => "HASH_" + password;
        public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
            => hashedPassword == "HASH_" + providedPassword || hashedPassword == providedPassword;
    }

    private PharmacyDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new PharmacyDbContext(options);
    }

    [Fact]
    public async Task NewPharmacy_PendingApproval_BlocksLoginWithAppropriateMessage()
    {
        // Arrange
        using var context = CreateDbContext();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(context, hasher);

        var pharmacy = new Pharmacy
        {
            Id = 10,
            Name = "Pending Cure Pharmacy",
            IsActive = false,
            Code = "STATUS:PENDING_APPROVAL"
        };
        var adminUser = new User
        {
            Id = 10,
            FullName = "Dr. Sameh",
            Email = "sameh@curepharmacy.com",
            PasswordHash = hasher.HashPassword(null!, "Secret123!"),
            Role = UserRole.Admin,
            IsActive = false,
            PharmacyId = 10
        };

        context.Pharmacies.Add(pharmacy);
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.ValidateCredentialsAsync("sameh@curepharmacy.com", "Secret123!");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("PharmacyPendingApproval", result.ErrorMessage);
    }

    [Fact]
    public async Task DeveloperApproval_ActivatesPharmacyAndAdminUser_AllowsSuccessfulLogin()
    {
        // Arrange
        using var context = CreateDbContext();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(context, hasher);

        var pharmacy = new Pharmacy
        {
            Id = 11,
            Name = "Al-Amal Pharmacy",
            IsActive = false,
            Code = "STATUS:PENDING_APPROVAL"
        };
        var adminUser = new User
        {
            Id = 11,
            FullName = "Dr. Mona",
            Email = "mona@alamal.com",
            PasswordHash = hasher.HashPassword(null!, "Pass123!"),
            Role = UserRole.Admin,
            IsActive = false,
            PharmacyId = 11
        };

        context.Pharmacies.Add(pharmacy);
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Simulate Developer Approval
        pharmacy.Code = "PHARM-0011";
        pharmacy.IsActive = true;
        adminUser.IsActive = true;
        await context.SaveChangesAsync();

        // Act
        var result = await authService.ValidateCredentialsAsync("mona@alamal.com", "Pass123!");

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal("mona@alamal.com", result.User.Email);
    }

    [Fact]
    public async Task DeveloperRejection_BlocksLoginWithRejectedMessage()
    {
        // Arrange
        using var context = CreateDbContext();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(context, hasher);

        var pharmacy = new Pharmacy
        {
            Id = 12,
            Name = "Rejected Meds",
            IsActive = false,
            Code = "STATUS:REJECTED"
        };
        var adminUser = new User
        {
            Id = 12,
            FullName = "Fake Applicant",
            Email = "applicant@rejected.com",
            PasswordHash = hasher.HashPassword(null!, "Pass123!"),
            Role = UserRole.Admin,
            IsActive = false,
            PharmacyId = 12
        };

        context.Pharmacies.Add(pharmacy);
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.ValidateCredentialsAsync("applicant@rejected.com", "Pass123!");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("PharmacyRegistrationRejected", result.ErrorMessage);
    }

    [Fact]
    public async Task SuspendedPharmacy_BlocksLoginWithSuspendedMessage()
    {
        // Arrange
        using var context = CreateDbContext();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(context, hasher);

        var pharmacy = new Pharmacy
        {
            Id = 13,
            Name = "Suspended Branch",
            IsActive = false,
            Code = "STATUS:SUSPENDED"
        };
        var adminUser = new User
        {
            Id = 13,
            FullName = "Branch Manager",
            Email = "manager@suspended.com",
            PasswordHash = hasher.HashPassword(null!, "Pass123!"),
            Role = UserRole.Admin,
            IsActive = true,
            PharmacyId = 13
        };

        context.Pharmacies.Add(pharmacy);
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.ValidateCredentialsAsync("manager@suspended.com", "Pass123!");

        // Assert
        Assert.False(result.Succeeded);
        Assert.Equal("PharmacySuspended", result.ErrorMessage);
    }

    [Fact]
    public async Task DeveloperUser_CanAlwaysLogin_RegardlessOfPharmacyStatus()
    {
        // Arrange
        using var context = CreateDbContext();
        var hasher = new FakePasswordHasher();
        var authService = new AuthService(context, hasher);

        var devUser = new User
        {
            Id = 999,
            FullName = "Global Developer",
            Email = "dev@system.local",
            PasswordHash = hasher.HashPassword(null!, "RootMaster123!"),
            Role = UserRole.Developer,
            IsActive = true,
            PharmacyId = null
        };

        context.Users.Add(devUser);
        await context.SaveChangesAsync();

        // Act
        var result = await authService.ValidateCredentialsAsync("dev@system.local", "RootMaster123!");

        // Assert
        Assert.True(result.Succeeded);
        Assert.NotNull(result.User);
        Assert.Equal(UserRole.Developer, result.User.Role);
    }
}
