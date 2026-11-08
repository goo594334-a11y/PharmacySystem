using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using PharmacySystem.Data;
using PharmacySystem.Extensions;
using PharmacySystem.Models.Entities;
using PharmacySystem.Models.ViewModels.Auth;
using PharmacySystem.Resources;
using PharmacySystem.Services.Auth;
using PharmacySystem.Services.Email;

namespace PharmacySystem.Controllers;

public class AccountController : Controller
{
    private readonly IAuthService _authService;
    private readonly IPasswordResetService _passwordResetService;
    private readonly IEmailService _emailService;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly PharmacyDbContext _dbContext;
    private readonly IWebHostEnvironment _environment;
    private readonly IStringLocalizer<SharedResource> _loc;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthService authService,
        IPasswordResetService passwordResetService,
        IEmailService emailService,
        IPasswordHasherService passwordHasher,
        PharmacyDbContext dbContext,
        IWebHostEnvironment environment,
        IStringLocalizer<SharedResource> loc,
        ILogger<AccountController> logger)
    {
        _authService = authService;
        _passwordResetService = passwordResetService;
        _emailService = emailService;
        _passwordHasher = passwordHasher;
        _dbContext = dbContext;
        _environment = environment;
        _loc = loc;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        // IMPORTANT: IgnoreQueryFilters() is mandatory here.
        // The global tenant query filter on User is evaluated BEFORE authentication is established.
        // An anonymous (unauthenticated) request has HasUserContext=true but IsAuthenticated=false,
        // which causes the filter to return zero rows for every user — making the system think
        // no admin exists and incorrectly redirecting all anonymous users to RegisterAdmin.
        // This check is a system-bootstrap guard, not tenant-scoped data access, so bypassing the
        // tenant filter is correct and intentional here.
        var hasActiveAdmin = await _dbContext.Users
            .IgnoreQueryFilters()
            .AnyAsync(u => u.Role == PharmacySystem.Enums.UserRole.Admin && u.IsActive);
        if (!hasActiveAdmin)
        {
            return RedirectToAction(nameof(RegisterAdmin));
        }

        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";

        var model = new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Email = string.Empty,
            Password = string.Empty
        };

        return View(model);
    }

    // ==========================================
    // FIRST-TIME ADMIN SETUP
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterAdmin()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        return View(new RegisterAdminViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdmin(RegisterAdminViewModel model)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        // IgnoreQueryFilters: email uniqueness check is a registration guard, not tenant-scoped
        var exists = await _dbContext.Users.IgnoreQueryFilters().AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Email), _loc["EmailAlreadyExists"]);
            return View(model);
        }

        var pharmacy = new Pharmacy
        {
            Name = model.PharmacyName.Trim(),
            Code = $"PHARM-{DateTime.UtcNow:yyyyMMddHHmm}",
            Phone = model.Phone?.Trim(),
            Address = model.Address?.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
        _dbContext.Pharmacies.Add(pharmacy);
        await _dbContext.SaveChangesAsync();

        pharmacy.Code = $"PHARM-{pharmacy.Id:D4}";

        var adminUser = new User
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            Role = PharmacySystem.Enums.UserRole.Admin,
            PharmacyId = pharmacy.Id,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, model.Password);

        _dbContext.Users.Add(adminUser);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            PharmacyId = pharmacy.Id,
            Action = "PharmacyRegistered",
            EntityName = nameof(Pharmacy),
            EntityId = pharmacy.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PharmacyCreatedSuccessfully"]);
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("LoginLimiter")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _authService.ValidateCredentialsAsync(
            model.Email,
            model.Password);

        if (!result.Succeeded || result.User == null)
        {
            var errorKey = result.ErrorMessage ?? "InvalidCredentials";
            var localizedError = _loc[errorKey];
            ModelState.AddModelError(string.Empty, localizedError);
            return View(model);
        }

        await _authService.SignInAsync(HttpContext, result.User, model.RememberMe);

        if (result.User.MustChangePassword)
        {
            TempData.SetWarning(_loc["Notice"], _loc["MustChangePasswordNotice"]);
            return RedirectToAction(nameof(ChangePassword));
        }

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["Welcome"]);

        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        try
        {
            await _authService.SignOutAsync(HttpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AccountController.Logout. Forcing cookie invalidation.");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        finally
        {
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public async Task<IActionResult> LogoutGet()
    {
        try
        {
            await _authService.SignOutAsync(HttpContext);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error in AccountController.LogoutGet. Forcing cookie invalidation.");
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        finally
        {
            Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
            Response.Headers.Pragma = "no-cache";
            Response.Headers.Expires = "0";
        }
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }

    // ==========================================
    // FORGOT PASSWORD & OTP WORKFLOW
    // ==========================================

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("PasswordRecoveryLimiter")]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var rawOtp = await _passwordResetService.CreateResetOtpAsync(normalizedEmail);

        if (!string.IsNullOrEmpty(rawOtp))
        {
            // Professional, localized HTML email template for 6-digit OTP
            var emailBody = $@"
<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
<meta charset='utf-8'>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 24px; }}
  .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 14px; padding: 36px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.06); border: 1px solid #e2e8f0; text-align: center; }}
  .badge-icon {{ display: inline-flex; width: 64px; height: 64px; border-radius: 50%; background: #e6fffa; color: #0d7377; align-items: center; justify-content: center; font-size: 28px; margin-bottom: 20px; }}
  .title {{ color: #0d7377; font-size: 22px; font-weight: 700; margin: 0 0 6px 0; }}
  .subtitle {{ color: #64748b; font-size: 14px; margin-bottom: 24px; }}
  .otp-container {{ background: #f1f5f9; border: 2px dashed #0d7377; border-radius: 10px; padding: 18px 24px; margin: 24px 0; display: inline-block; }}
  .otp-code {{ font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #0d7377; font-family: 'Consolas', 'Courier New', monospace; }}
  .notice {{ background-color: #fef3c7; border: 1px solid #fde68a; color: #92400e; padding: 12px 16px; border-radius: 8px; font-size: 13px; margin-top: 20px; text-align: right; }}
  .footer {{ font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 20px; margin-top: 28px; text-align: center; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='badge-icon'>&#128272;</div>
    <div class='title'>نظام إدارة الصيدلية - رمز التحقق (OTP)</div>
    <div class='subtitle'>Pharmacy Management System - Aegis Rx Suite</div>
    <p style='color: #334155; font-size: 15px; line-height: 1.6; text-align: right;'>
      مرحبًا،<br>
      تلقينا طلبًا لإعادة تعيين كلمة المرور الخاصة بحسابك. استخدم رمز التحقق التالي لمرة واحدة لإتمام العملية:
    </p>
    <div class='otp-container'>
      <span class='otp-code'>{rawOtp}</span>
    </div>
    <div class='notice'>
      <strong>&#9888; تنبيه أمني:</strong><br>
      • هذا الرمز صالح لمدة <strong>60 ثانية (دقيقة واحدة)</strong> فقط.<br>
      • صالح للاستخدام <strong>مرة واحدة</strong> فقط.<br>
      • لا تشارك هذا الرمز مع أي شخص إطلاقاً.
    </div>
    <p style='font-size: 13px; color: #64748b; margin-top: 20px; text-align: right;'>
      إذا لم تقم بطلب إعادة تعيين كلمة المرور، يمكنك تجاهل هذه الرسالة بأمان وستظل بياناتك محمية.
    </p>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} Pharmacy Management System. All rights reserved.
    </div>
  </div>
</body>
</html>";

            var emailSent = await _emailService.SendEmailAsync(
                normalizedEmail,
                "رمز التحقق لإعادة تعيين كلمة المرور - OTP",
                emailBody);

            if (!emailSent)
            {
                _logger.LogWarning("Email delivery failed for [{Email}]. SMTP may be unconfigured, credentials invalid, or server unreachable.", normalizedEmail);
            }
            else
            {
                _logger.LogInformation("Password reset OTP email sent successfully to [{Email}].", normalizedEmail);
            }
        }

        // Generic neutral flow: redirect to VerifyOtp with the entered email to protect against account enumeration
        return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyOtp(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var remainingSeconds = await _passwordResetService.GetRemainingValiditySecondsAsync(normalizedEmail);

        return View(new VerifyOtpViewModel
        {
            Email = normalizedEmail,
            RemainingSeconds = remainingSeconds > 0 ? remainingSeconds : 60
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("PasswordRecoveryLimiter")]
    public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
    {
        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        model.RemainingSeconds = await _passwordResetService.GetRemainingValiditySecondsAsync(normalizedEmail);

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var (result, resetToken, remaining) = await _passwordResetService.VerifyOtpAsync(normalizedEmail, model.OtpCode);

        switch (result)
        {
            case OtpVerificationResult.Success when !string.IsNullOrEmpty(resetToken):
                TempData.SetSuccess("نجحت العملية", "تم التحقق من الرمز بنجاح. يمكنك الآن كتابة كلمة المرور الجديدة.");
                return RedirectToAction(nameof(ResetPassword), new { email = normalizedEmail, token = resetToken });

            case OtpVerificationResult.MaxAttemptsExceeded:
                ModelState.AddModelError(string.Empty, "تم تجاوز الحد الأقصى للمحاولات الخاطئة (5 محاولات). تم إبطال الرمز لحماية حسابك، يرجى طلب رمز جديد.");
                return View(model);

            case OtpVerificationResult.CooldownActive:
                ModelState.AddModelError(string.Empty, "يرجى الانتظار 60 ثانية قبل طلب رمز جديد.");
                return View(model);

            default:
                var errorMsg = remaining > 0
                    ? $"رمز التحقق غير صحيح أو انتهت صلاحيته. متبقي لديك {remaining} محاولات."
                    : "رمز التحقق غير صحيح أو انتهت صلاحيته (صلاحية الرمز 60 ثانية).";
                ModelState.AddModelError(string.Empty, errorMsg);
                return View(model);
        }
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("PasswordRecoveryLimiter")]
    public async Task<IActionResult> ResendOtp(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var rawOtp = await _passwordResetService.CreateResetOtpAsync(normalizedEmail);

        if (!string.IsNullOrEmpty(rawOtp))
        {
            var emailBody = $@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='utf-8'></head>
<body style='font-family: Segoe UI, sans-serif; background-color: #f8fafc; padding: 24px;'>
  <div style='max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 14px; padding: 36px; border: 1px solid #e2e8f0; text-align: center;'>
    <h2 style='color: #0d7377;'>نظام إدارة الصيدلية - رمز التحقق (OTP)</h2>
    <p style='text-align: right;'>رمز التحقق الجديد الخاص بك هو:</p>
    <div style='background: #f1f5f9; border: 2px dashed #0d7377; border-radius: 10px; padding: 18px 24px; margin: 20px 0;'>
      <span style='font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #0d7377; font-family: monospace;'>{rawOtp}</span>
    </div>
    <p style='color: #92400e; font-size: 13px; text-align: right;'>• هذا الرمز صالح لمدة 60 ثانية (دقيقة واحدة) فقط.<br>• لا تشارك الرمز مع أي شخص.</p>
  </div>
</body></html>";

            await _emailService.SendEmailAsync(normalizedEmail, "رمز التحقق لإعادة تعيين كلمة المرور - OTP", emailBody);
            TempData.SetSuccess("نجحت العملية", "تم إرسال رمز تحقق جديد إلى بريدك الإلكتروني.");
        }
        else
        {
            TempData.SetWarning("تنبيه", "يرجى الانتظار حتى انتهاء فترة التهدئة (60 ثانية) قبل إعادة الإرسال.");
        }

        return RedirectToAction(nameof(VerifyOtp), new { email = normalizedEmail });
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData.SetError(_loc["OperationFailed"], _loc["InvalidResetToken"]);
            return RedirectToAction(nameof(Login));
        }

        var isValid = await _passwordResetService.ValidateResetTokenAsync(email, token);
        if (!isValid)
        {
            TempData.SetError(_loc["OperationFailed"], _loc["InvalidResetToken"]);
            return RedirectToAction(nameof(Login));
        }

        return View(new ResetPasswordViewModel
        {
            Email = email,
            Token = token
        });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetSuccess = await _passwordResetService.ResetPasswordAsync(
            model.Email,
            model.Token,
            model.NewPassword);

        if (!resetSuccess)
        {
            ModelState.AddModelError(string.Empty, _loc["InvalidResetToken"]);
            return View(model);
        }

        return RedirectToAction(nameof(ResetPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ResetPasswordConfirmation()
    {
        return View();
    }

    /// <summary>
    /// Explicit, authorized email test endpoint available in Development or to authenticated Administrators.
    /// Safely tests sending one test message from the configured sender to the specified test recipient.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TestEmail(string? recipient = null)
    {
        if (!_environment.IsDevelopment() && !User.IsInRole(PharmacySystem.Enums.UserRole.Admin.ToString()))
        {
            return Forbid();
        }

        var targetRecipient = string.IsNullOrWhiteSpace(recipient) ? "goo594334@gmail.com" : recipient.Trim();

        if (!_emailService.IsConfigured())
        {
            return Json(new
            {
                Success = false,
                Message = "SMTP settings are unconfigured or incomplete. Please set your Gmail App Password in User Secrets or environment variables.",
                Recipient = targetRecipient,
                Sender = "testgemail527@gmail.com"
            });
        }

        try
        {
            var testBody = $@"
<div style='font-family: Arial, sans-serif; padding: 20px; border: 1px solid #14b8a6; border-radius: 8px;'>
  <h2 style='color: #0d7377;'>Pharmacy Management System - Test Message</h2>
  <p>This is a verification email triggered from the Pharmacy System environment.</p>
  <p><strong>Sender:</strong> testgemail527@gmail.com<br>
  <strong>Recipient:</strong> {targetRecipient}<br>
  <strong>Timestamp:</strong> {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
  <p style='color: #10b981; font-weight: bold;'>SMTP connection and credentials verified successfully!</p>
</div>";

            var sent = await _emailService.SendEmailAsync(
                targetRecipient,
                "اختبار اتصال البريد الإلكتروني - Pharmacy System SMTP Test",
                testBody);

            return Json(new
            {
                Success = sent,
                Message = sent ? "SMTP server accepted the message for delivery." : "Failed to send email. Check application logs.",
                Recipient = targetRecipient,
                Sender = "testgemail527@gmail.com"
            });
        }
        catch (Exception ex)
        {
            return Json(new
            {
                Success = false,
                Message = "SMTP Error: " + ex.Message,
                Recipient = targetRecipient
            });
        }
    }

    // ==========================================
    // CHANGE PASSWORD (AUTHENTICATED)
    // ==========================================

    [HttpGet]
    [Authorize]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out var userId))
        {
            return RedirectToAction(nameof(Login));
        }

        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null || !user.IsActive)
        {
            await _authService.SignOutAsync(HttpContext);
            return RedirectToAction(nameof(Login));
        }

        // Verify current password
        var isCurrentValid = _passwordHasher.VerifyPassword(user, user.PasswordHash, model.CurrentPassword);
        if (!isCurrentValid)
        {
            ModelState.AddModelError(nameof(model.CurrentPassword), _loc["IncorrectCurrentPassword"]);
            return View(model);
        }

        // Hash and store new password
        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            PharmacyId = user.PharmacyId ?? 1,
            Action = "ChangePassword",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        // Refresh authentication cookie with updated claims
        await _authService.SignInAsync(HttpContext, user, false);

        TempData.SetSuccess(_loc["OperationSuccess"], _loc["PasswordChangedSuccessfully"]);
        return RedirectToAction("Index", "Home");
    }
}
