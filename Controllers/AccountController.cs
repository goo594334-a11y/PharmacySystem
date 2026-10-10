using System.Security.Claims;
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
        // First-run experience: If no Admin exists, redirect to first-time setup
        var hasAdmin = await _dbContext.Users.AnyAsync(u => u.Role == PharmacySystem.Enums.UserRole.Admin);
        if (!hasAdmin)
        {
            return RedirectToAction(nameof(RegisterAdmin));
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
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
        var hasAdmin = await _dbContext.Users.AnyAsync(u => u.Role == PharmacySystem.Enums.UserRole.Admin);
        if (hasAdmin)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction(nameof(Login));
        }

        return View(new RegisterAdminViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterAdmin(RegisterAdminViewModel model)
    {
        var hasAdmin = await _dbContext.Users.AnyAsync(u => u.Role == PharmacySystem.Enums.UserRole.Admin);
        if (hasAdmin)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }
            return RedirectToAction(nameof(Login));
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var exists = await _dbContext.Users.AnyAsync(u => u.Email.ToLower() == normalizedEmail);
        if (exists)
        {
            ModelState.AddModelError(nameof(model.Email), _loc["EmailAlreadyExists"]);
            return View(model);
        }

        var adminUser = new User
        {
            FullName = model.FullName.Trim(),
            Email = normalizedEmail,
            Role = PharmacySystem.Enums.UserRole.Admin,
            IsActive = true,
            MustChangePassword = false,
            CreatedAt = DateTime.UtcNow
        };

        adminUser.PasswordHash = _passwordHasher.HashPassword(adminUser, model.Password);

        _dbContext.Users.Add(adminUser);
        _dbContext.AuditLogs.Add(new AuditLog
        {
            Action = "FirstAdminSetup",
            EntityName = nameof(User),
            EntityId = normalizedEmail,
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Sign in immediately
        await _authService.SignInAsync(HttpContext, adminUser, isPersistent: true);
        TempData.SetSuccess(_loc["OperationSuccess"], _loc["Welcome"]);

        return RedirectToAction("Index", "Home");
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
        await _authService.SignOutAsync(HttpContext);
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
        return RedirectToAction("Login", "Account");
    }

    [HttpGet]
    public async Task<IActionResult> LogoutGet()
    {
        await _authService.SignOutAsync(HttpContext);
        Response.Headers.CacheControl = "no-cache, no-store, must-revalidate";
        Response.Headers.Pragma = "no-cache";
        Response.Headers.Expires = "0";
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
    // FORGOT PASSWORD WORKFLOW
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

        var token = await _passwordResetService.CreateResetTokenAsync(model.Email);
        if (!string.IsNullOrEmpty(token))
        {
            var resetLink = Url.Action("ResetPassword", "Account", new { email = model.Email, token }, Request.Scheme);
            
            // Professional, localized HTML email template with 15-minute expiration notice
            var emailBody = $@"
<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
  body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 20px; }}
  .card {{ max-width: 520px; margin: 0 auto; background: #ffffff; border-radius: 12px; padding: 32px; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); border: 1px solid #e2e8f0; }}
  .header {{ text-align: center; margin-bottom: 24px; }}
  .title {{ color: #0d7377; font-size: 20px; font-weight: bold; margin: 0; }}
  .subtitle {{ color: #64748b; font-size: 13px; margin-top: 4px; }}
  .btn {{ display: inline-block; background-color: #0d7377; color: #ffffff !important; text-decoration: none; padding: 12px 28px; border-radius: 8px; font-weight: 600; font-size: 14px; margin: 20px 0; }}
  .footer {{ font-size: 12px; color: #94a3b8; border-top: 1px solid #e2e8f0; padding-top: 16px; margin-top: 24px; text-align: center; }}
  .notice {{ background-color: #fef3c7; border: 1px solid #fde68a; color: #92400e; padding: 10px 14px; border-radius: 6px; font-size: 12px; margin-top: 16px; }}
</style>
</head>
<body>
  <div class='card'>
    <div class='header'>
      <div class='title'>Pharmacy Management System</div>
      <div class='subtitle'>Aegis Rx Suite</div>
    </div>
    <p>{_loc["PasswordResetEmailGreeting"]}</p>
    <p>{_loc["PasswordResetInstructions"]}</p>
    <div style='text-align: center;'>
      <a href='{resetLink}' class='btn'>{_loc["ResetPassword"]}</a>
    </div>
    <div class='notice'>
      <strong>{_loc["WarningWindow"]}:</strong> {_loc["TokenExpiresIn15Minutes"]}
    </div>
    <p style='font-size: 12px; color: #64748b; margin-top: 16px;'>{_loc["PasswordResetIgnorePrompt"]}</p>
    <div class='footer'>
      &copy; {DateTime.UtcNow.Year} Pharmacy Management System. All rights reserved.
    </div>
  </div>
</body>
</html>";

            await _emailService.SendEmailAsync(
                model.Email,
                _loc["ResetPassword"].Value,
                emailBody);

            _logger.LogInformation("Password reset requested for [{Email}].", model.Email);

            // DIRECT RESET LINK RULE: Never in production. Only when in Development AND email sending is not configured.
            if (_environment.IsDevelopment() && !_emailService.IsConfigured())
            {
                TempData["ResetLinkPreview"] = resetLink;
            }
        }

        // Generic neutral response to prevent user enumeration
        return RedirectToAction(nameof(ForgotPasswordConfirmation));
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult ForgotPasswordConfirmation()
    {
        return View();
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
