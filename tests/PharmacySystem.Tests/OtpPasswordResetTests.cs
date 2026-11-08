using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PharmacySystem.Data;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;
using PharmacySystem.Services.Email;
using Xunit;

namespace PharmacySystem.Tests;

public class OtpPasswordResetTests
{
    private (PharmacyDbContext db, Mock<IPasswordHasherService> hasherMock, Mock<ILogger<PasswordResetService>> loggerMock) CreateDependencies()
    {
        var options = new DbContextOptionsBuilder<PharmacyDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var db = new PharmacyDbContext(options);
        var hasherMock = new Mock<IPasswordHasherService>();
        var loggerMock = new Mock<ILogger<PasswordResetService>>();

        return (db, hasherMock, loggerMock);
    }

    private async Task<User> SeedActiveUserAsync(PharmacyDbContext db, string email = "pharmacist@example.com")
    {
        var pharmacy = new Pharmacy { Id = 1, Name = "Test Pharmacy", IsActive = true, CreatedAt = DateTime.UtcNow };
        db.Pharmacies.Add(pharmacy);

        var user = new User
        {
            Id = 1,
            FullName = "Ahmed Test",
            Email = email.ToLowerInvariant(),
            PasswordHash = "InitialHashedPassword",
            Role = Enums.UserRole.Pharmacist,
            PharmacyId = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    [Fact]
    public async Task CreateResetOtp_WithActiveUser_Generates6DigitOtp()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "user@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("user@test.local");

        Assert.NotNull(otp);
        Assert.Equal(6, otp.Length);
        Assert.Matches(@"^\d{6}$", otp);

        var tokenRecord = await db.PasswordResetTokens.FirstOrDefaultAsync();
        Assert.NotNull(tokenRecord);
        Assert.StartsWith("OTP:v1:", tokenRecord.TokenHash);
        Assert.True(tokenRecord.ExpiresAt > DateTime.UtcNow);
        Assert.True((tokenRecord.ExpiresAt - DateTime.UtcNow).TotalSeconds <= 65);
    }

    [Fact]
    public async Task CreateResetOtp_ValidityIsStrictly60Seconds()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "sixtysec@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("sixtysec@test.local");
        Assert.NotNull(otp);

        var remainingSeconds = await service.GetRemainingValiditySecondsAsync("sixtysec@test.local");
        Assert.InRange(remainingSeconds, 55, 60);

        var tokenRecord = await db.PasswordResetTokens.FirstAsync();
        var diff = (tokenRecord.ExpiresAt - tokenRecord.CreatedAt).TotalSeconds;
        Assert.Equal(60, diff, 1.0);
    }

    [Fact]
    public async Task CreateResetOtp_WithNonExistentUser_ReturnsNullWithoutThrowing()
    {
        var (db, hasher, logger) = CreateDependencies();
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        // Account enumeration prevention: does not disclose non-existence
        var otp = await service.CreateResetOtpAsync("nonexistent@domain.com");

        Assert.Null(otp);
        var count = await db.PasswordResetTokens.CountAsync();
        Assert.Equal(0, count);
    }

    [Fact]
    public async Task CreateResetOtp_WhenRequestedTwiceWithinCooldown_IsThrottled()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "throttle@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var firstOtp = await service.CreateResetOtpAsync("throttle@test.local");
        Assert.NotNull(firstOtp);

        // Immediately request again (within 60s cooldown)
        var secondOtp = await service.CreateResetOtpAsync("throttle@test.local");
        Assert.Null(secondOtp); // Throttled
    }

    [Fact]
    public async Task VerifyOtp_WithValidOtp_ReturnsSuccessAndResetToken()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "valid@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("valid@test.local");
        Assert.NotNull(otp);

        var (result, resetToken, remaining) = await service.VerifyOtpAsync("valid@test.local", otp);

        Assert.Equal(OtpVerificationResult.Success, result);
        Assert.NotNull(resetToken);
        Assert.NotEmpty(resetToken);

        // Verify that token transitioned to VERIFIED state
        var tokenRecord = await db.PasswordResetTokens.FirstAsync();
        Assert.Equal($"VERIFIED:v1:{resetToken}", tokenRecord.TokenHash);
    }

    [Fact]
    public async Task VerifyOtp_WithInvalidOtp_DecrementsRemainingAttempts()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "attempts@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("attempts@test.local");
        Assert.NotNull(otp);

        var wrongOtp = otp == "123456" ? "654321" : "123456";
        var (result, resetToken, remaining) = await service.VerifyOtpAsync("attempts@test.local", wrongOtp);

        Assert.Equal(OtpVerificationResult.InvalidOrExpired, result);
        Assert.Null(resetToken);
        Assert.Equal(4, remaining); // 5 - 1 = 4 remaining
    }

    [Fact]
    public async Task VerifyOtp_Exceeding5Attempts_LocksOutToken()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "lockout@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("lockout@test.local");
        Assert.NotNull(otp);

        // Fail 5 times
        OtpVerificationResult lastResult = OtpVerificationResult.InvalidOrExpired;
        for (int i = 0; i < 5; i++)
        {
            var res = await service.VerifyOtpAsync("lockout@test.local", "000000");
            lastResult = res.Result;
        }

        Assert.Equal(OtpVerificationResult.MaxAttemptsExceeded, lastResult);

        // Subsequent attempt, even with the correct OTP, must fail because token was locked/used
        var retryWithCorrect = await service.VerifyOtpAsync("lockout@test.local", otp);
        Assert.NotEqual(OtpVerificationResult.Success, retryWithCorrect.Result);
    }

    [Fact]
    public async Task VerifyOtp_WithExpiredOtp_FailsVerification()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "expired@test.local");
        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("expired@test.local");
        Assert.NotNull(otp);

        // Manually expire the token in DB
        var tokenRecord = await db.PasswordResetTokens.FirstAsync();
        tokenRecord.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
        await db.SaveChangesAsync();

        var (result, resetToken, _) = await service.VerifyOtpAsync("expired@test.local", otp);
        Assert.Equal(OtpVerificationResult.InvalidOrExpired, result);
        Assert.Null(resetToken);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_UpdatesPasswordAndInvalidatesToken()
    {
        var (db, hasher, logger) = CreateDependencies();
        var user = await SeedActiveUserAsync(db, "reset@test.local");
        hasher.Setup(h => h.HashPassword(It.IsAny<User>(), "NewSecurePass123!"))
              .Returns("NewPasswordHash_Calculated");

        var service = new PasswordResetService(db, hasher.Object, logger.Object);

        var otp = await service.CreateResetOtpAsync("reset@test.local");
        var verifyResult = await service.VerifyOtpAsync("reset@test.local", otp!);
        var resetToken = verifyResult.ResetToken;

        var resetSuccess = await service.ResetPasswordAsync("reset@test.local", resetToken!, "NewSecurePass123!");
        Assert.True(resetSuccess);

        // Verify DB updates
        var updatedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal("NewPasswordHash_Calculated", updatedUser!.PasswordHash);

        // Verify token is invalidated (UsedAt set)
        var tokenRecord = await db.PasswordResetTokens.FirstAsync();
        Assert.NotNull(tokenRecord.UsedAt);

        // Reusing the same resetToken must fail
        var secondReset = await service.ResetPasswordAsync("reset@test.local", resetToken!, "AnotherPass456!");
        Assert.False(secondReset);
    }

    [Fact]
    public async Task EmailService_WhenUnconfigured_IsConfiguredReturnsFalse()
    {
        var options = Options.Create(new EmailSettings
        {
            Host = "smtp.gmail.com",
            Port = 587,
            Username = "testgemail527@gmail.com",
            Password = "", // Empty password
            FromEmail = "testgemail527@gmail.com"
        });

        var logger = new Mock<ILogger<EmailService>>();
        var emailService = new EmailService(options, logger.Object);

        Assert.False(emailService.IsConfigured());

        // SendEmailAsync returns false safely without throwing exception
        var sendResult = await emailService.SendEmailAsync("goo594334@gmail.com", "Test Subject", "Test Body");
        Assert.False(sendResult);
    }

    [Fact]
    public async Task EmailService_WithValidSettings_IsConfiguredReturnsTrue()
    {
        var options = Options.Create(new EmailSettings
        {
            Host = "smtp.gmail.com",
            Port = 587,
            Username = "testgemail527@gmail.com",
            AppPassword = "abcd efgh ijkl mnop",
            FromEmail = "testgemail527@gmail.com"
        });

        var logger = new Mock<ILogger<EmailService>>();
        var emailService = new EmailService(options, logger.Object);

        Assert.True(emailService.IsConfigured());
    }

    [Fact]
    public async Task ForgotPassword_CallsEmailService_WhenOtpGenerated()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "forgot_test@pharmacy.local");

        var resetServiceMock = new Mock<IPasswordResetService>();
        resetServiceMock.Setup(s => s.CreateResetOtpAsync("forgot_test@pharmacy.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync("654321");

        var emailServiceMock = new Mock<IEmailService>();
        emailServiceMock.Setup(e => e.SendEmailAsync(
            "forgot_test@pharmacy.local",
            It.IsAny<string>(),
            It.IsAny<string>(),
            true,
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var authServiceMock = new Mock<IAuthService>();
        var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var locMock = new Mock<Microsoft.Extensions.Localization.IStringLocalizer<PharmacySystem.Resources.SharedResource>>();
        var ctrlLoggerMock = new Mock<ILogger<PharmacySystem.Controllers.AccountController>>();

        var controller = new PharmacySystem.Controllers.AccountController(
            authServiceMock.Object,
            resetServiceMock.Object,
            emailServiceMock.Object,
            hasher.Object,
            db,
            envMock.Object,
            locMock.Object,
            ctrlLoggerMock.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>().Object)
        };

        var model = new PharmacySystem.Models.ViewModels.Auth.ForgotPasswordViewModel
        {
            Email = "forgot_test@pharmacy.local"
        };

        var result = await controller.ForgotPassword(model);

        // Verify email service was actually called with recipient
        emailServiceMock.Verify(e => e.SendEmailAsync(
            "forgot_test@pharmacy.local",
            It.IsAny<string>(),
            It.Is<string>(body => body.Contains("654321")),
            true,
            It.IsAny<CancellationToken>()), Times.Once);

        // Verify TempData does NOT contain DevOtpPreview (never shown on screen)
        Assert.False(controller.TempData.ContainsKey("DevOtpPreview"));

        // Verify redirect to VerifyOtp
        var redirectResult = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(result);
        Assert.Equal("VerifyOtp", redirectResult.ActionName);
    }

    [Fact]
    public async Task ForgotPassword_WhenEmailSendingFails_DoesNotThrowAndDoesNotLeakOtp()
    {
        var (db, hasher, logger) = CreateDependencies();
        await SeedActiveUserAsync(db, "smtp_fail@pharmacy.local");

        var resetServiceMock = new Mock<IPasswordResetService>();
        resetServiceMock.Setup(s => s.CreateResetOtpAsync("smtp_fail@pharmacy.local", It.IsAny<CancellationToken>()))
            .ReturnsAsync("112233");

        var emailServiceMock = new Mock<IEmailService>();
        // Simulate SMTP failure (e.g. auth fail or network error)
        emailServiceMock.Setup(e => e.SendEmailAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var authServiceMock = new Mock<IAuthService>();
        var envMock = new Mock<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>();
        envMock.Setup(e => e.EnvironmentName).Returns("Development");
        var locMock = new Mock<Microsoft.Extensions.Localization.IStringLocalizer<PharmacySystem.Resources.SharedResource>>();
        var ctrlLoggerMock = new Mock<ILogger<PharmacySystem.Controllers.AccountController>>();

        var controller = new PharmacySystem.Controllers.AccountController(
            authServiceMock.Object,
            resetServiceMock.Object,
            emailServiceMock.Object,
            hasher.Object,
            db,
            envMock.Object,
            locMock.Object,
            ctrlLoggerMock.Object)
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext()
            },
            TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
                new Microsoft.AspNetCore.Http.DefaultHttpContext(),
                new Mock<Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider>().Object)
        };

        var model = new PharmacySystem.Models.ViewModels.Auth.ForgotPasswordViewModel
        {
            Email = "smtp_fail@pharmacy.local"
        };

        var result = await controller.ForgotPassword(model);

        // OTP is NOT shown on screen even if SMTP fails
        Assert.False(controller.TempData.ContainsKey("DevOtpPreview"));

        var redirectResult = Assert.IsType<Microsoft.AspNetCore.Mvc.RedirectToActionResult>(result);
        Assert.Equal("VerifyOtp", redirectResult.ActionName);
    }
}
