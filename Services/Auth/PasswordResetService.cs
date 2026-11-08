using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services.Auth;

public enum OtpVerificationResult
{
    Success,
    InvalidOrExpired,
    MaxAttemptsExceeded,
    CooldownActive
}

public interface IPasswordResetService
{
    /// <summary>
    /// Generates a cryptographically secure 6-digit OTP for the specified email and records it.
    /// Returns the raw 6-digit OTP if user exists and is active, or null (preventing user enumeration).
    /// </summary>
    Task<string?> CreateResetOtpAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the entered 6-digit OTP against expiration, max attempts, and hash.
    /// If successful, returns a secure single-use reset token to authorize the password change.
    /// </summary>
    Task<(OtpVerificationResult Result, string? ResetToken, int RemainingAttempts)> VerifyOtpAsync(string email, string otpCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates the authorized reset token before displaying or processing the password change form.
    /// </summary>
    Task<bool> ValidateResetTokenAsync(string email, string token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the remaining validity in seconds for the user's active OTP token.
    /// </summary>
    Task<int> GetRemainingValiditySecondsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the user's password using the authorized reset token, hashing the new password,
    /// invalidating the token, and recording an audit trail.
    /// </summary>
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}

public class PasswordResetService : IPasswordResetService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<PasswordResetService> _logger;

    public const int MaxVerificationAttempts = 5;
    public const int OtpValiditySeconds = 60;
    public const int ResendCooldownSeconds = 60;

    public PasswordResetService(
        PharmacyDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger<PasswordResetService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<string?> CreateResetOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        // IgnoreQueryFilters: password reset is an anonymous pre-auth operation;
        // the tenant filter would return no users when called from an unauthenticated context.
        var user = await _dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null || !user.IsActive)
        {
            // Do not disclose account absence
            return null;
        }

        // Check for resend cooldown (1 minute between requests)
        var latestActiveToken = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestActiveToken != null)
        {
            var secondsSinceLast = (DateTime.UtcNow - latestActiveToken.CreatedAt).TotalSeconds;
            if (secondsSinceLast < ResendCooldownSeconds)
            {
                _logger.LogWarning("OTP generation for [{Email}] throttled by cooldown ({Seconds:F0}s < {Cooldown}s).",
                    normalizedEmail, secondsSinceLast, ResendCooldownSeconds);
                return null;
            }
        }

        // Invalidate any previous unused tokens for this user (only latest OTP is valid)
        var existingTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var existing in existingTokens)
        {
            existing.UsedAt = DateTime.UtcNow;
        }

        // Generate cryptographically secure 6-digit OTP: 100000 to 999999
        var otpNumber = RandomNumberGenerator.GetInt32(100000, 1000000);
        var rawOtp = otpNumber.ToString("D6");

        // Format stored hash as: OTPHASH:Salt:FailedAttempts:ResetSessionToken:HashOfOtp
        // This embeds security metadata into TokenHash (nvarchar(256)) without altering the database schema!
        var salt = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        var otpHash = ComputeHash($"{rawOtp}:{salt}:{normalizedEmail}");
        var resetSessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

        var compositePayload = $"OTP:v1:{salt}:0:{resetSessionToken}:{otpHash}";

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = compositePayload,
            ExpiresAt = DateTime.UtcNow.AddSeconds(OtpValiditySeconds),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset OTP generated for user [{Email}]. Expires in {Seconds} seconds.",
            normalizedEmail, OtpValiditySeconds);

        return rawOtp;
    }

    public async Task<(OtpVerificationResult Result, string? ResetToken, int RemainingAttempts)> VerifyOtpAsync(
        string email, string otpCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(otpCode))
            return (OtpVerificationResult.InvalidOrExpired, null, 0);

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var normalizedOtp = otpCode.Trim();

        var entry = await _dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .Where(t => t.User.Email.ToLower() == normalizedEmail && t.UsedAt == null)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null || entry.ExpiresAt <= DateTime.UtcNow)
        {
            return (OtpVerificationResult.InvalidOrExpired, null, 0);
        }

        // Parse composite payload: "OTP:v1:{salt}:{failedAttempts}:{resetSessionToken}:{otpHash}"
        var parts = entry.TokenHash.Split(':');
        if (parts.Length != 6 || parts[0] != "OTP" || parts[1] != "v1")
        {
            // Legacy token or malformed format
            return (OtpVerificationResult.InvalidOrExpired, null, 0);
        }

        var salt = parts[2];
        int.TryParse(parts[3], out var failedAttempts);
        var resetSessionToken = parts[4];
        var expectedHash = parts[5];

        if (failedAttempts >= MaxVerificationAttempts)
        {
            // Lock out this OTP entry
            entry.UsedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("OTP for user [{Email}] locked out after {MaxAttempts} failed attempts.",
                normalizedEmail, MaxVerificationAttempts);
            return (OtpVerificationResult.MaxAttemptsExceeded, null, 0);
        }

        var candidateHash = ComputeHash($"{normalizedOtp}:{salt}:{normalizedEmail}");
        var isMatch = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(candidateHash),
            Convert.FromHexString(expectedHash));

        if (!isMatch)
        {
            failedAttempts++;
            var remaining = Math.Max(0, MaxVerificationAttempts - failedAttempts);

            if (failedAttempts >= MaxVerificationAttempts)
            {
                entry.UsedAt = DateTime.UtcNow; // permanently invalidate on max attempts
            }
            else
            {
                entry.TokenHash = $"OTP:v1:{salt}:{failedAttempts}:{resetSessionToken}:{expectedHash}";
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            _logger.LogWarning("Invalid OTP entered for user [{Email}]. Attempts remaining: {Remaining}.",
                normalizedEmail, remaining);

            return failedAttempts >= MaxVerificationAttempts
                ? (OtpVerificationResult.MaxAttemptsExceeded, null, 0)
                : (OtpVerificationResult.InvalidOrExpired, null, remaining);
        }

        // Verification SUCCESS:
        // Transition token to "VERIFIED" state with resetSessionToken so password can be changed securely.
        // Format: "VERIFIED:v1:{resetSessionToken}"
        entry.TokenHash = $"VERIFIED:v1:{resetSessionToken}";
        entry.ExpiresAt = DateTime.UtcNow.AddMinutes(15);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OTP verified successfully for user [{Email}]. Issued reset session token.", normalizedEmail);
        return (OtpVerificationResult.Success, resetSessionToken, MaxVerificationAttempts);
    }

    public async Task<int> GetRemainingValiditySecondsAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return 0;
        var normalizedEmail = email.Trim().ToLowerInvariant();
        // IgnoreQueryFilters: anonymous pre-auth call; tenant filter returns no rows when not authenticated.
        var entry = await _dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .Where(t => t.User.Email.ToLower() == normalizedEmail && t.UsedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (entry == null) return 0;
        var remaining = (int)Math.Max(0, Math.Ceiling((entry.ExpiresAt - DateTime.UtcNow).TotalSeconds));
        return Math.Min(remaining, OtpValiditySeconds);
    }

    public async Task<bool> ValidateResetTokenAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var expectedVerifiedPayload = $"VERIFIED:v1:{token.Trim()}";

        var entry = await _dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == expectedVerifiedPayload &&
                t.User.Email.ToLower() == normalizedEmail &&
                t.ExpiresAt > DateTime.UtcNow &&
                t.UsedAt == null,
                cancellationToken);

        return entry != null;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var expectedVerifiedPayload = $"VERIFIED:v1:{token.Trim()}";

        var entry = await _dbContext.PasswordResetTokens
            .IgnoreQueryFilters()
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == expectedVerifiedPayload &&
                t.User.Email.ToLower() == normalizedEmail &&
                t.ExpiresAt > DateTime.UtcNow &&
                t.UsedAt == null,
                cancellationToken);

        if (entry == null) return false;

        var user = entry.User;
        if (!user.IsActive) return false;

        // Hash new password securely with application's password hasher
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidate token (single-use guarantee)
        entry.UsedAt = DateTime.UtcNow;

        // Record audit trail
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            PharmacyId = user.PharmacyId ?? 1,
            Action = "ResetPasswordViaOtp",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successfully completed via OTP for user [{Email}].", normalizedEmail);
        return true;
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
