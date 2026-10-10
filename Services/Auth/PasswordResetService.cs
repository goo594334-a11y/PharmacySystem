using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using PharmacySystem.Data;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services.Auth;

public interface IPasswordResetService
{
    Task<string?> CreateResetTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<bool> ValidateResetTokenAsync(string email, string token, CancellationToken cancellationToken = default);
    Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default);
}

public class PasswordResetService : IPasswordResetService
{
    private readonly PharmacyDbContext _dbContext;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ILogger<PasswordResetService> _logger;

    public PasswordResetService(
        PharmacyDbContext dbContext,
        IPasswordHasherService passwordHasher,
        ILogger<PasswordResetService> logger)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<string?> CreateResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail, cancellationToken);

        if (user == null || !user.IsActive)
        {
            // Do not disclose account absence
            return null;
        }

        // Invalidate any previous unused tokens for this user
        var existingTokens = await _dbContext.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var existing in existingTokens)
        {
            existing.UsedAt = DateTime.UtcNow;
        }

        // Generate 32 bytes cryptographically secure random token
        var randomBytes = RandomNumberGenerator.GetBytes(32);
        var rawToken = Convert.ToHexString(randomBytes);

        // Store HASH of the token, not the raw token
        var tokenHash = ComputeHash(rawToken);

        _dbContext.PasswordResetTokens.Add(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            CreatedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset token generated for user [{Email}]. Expires in 15 minutes.", normalizedEmail);
        return rawToken;
    }

    public async Task<bool> ValidateResetTokenAsync(string email, string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var tokenHash = ComputeHash(token);

        var entry = await _dbContext.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.User.Email.ToLower() == normalizedEmail &&
                t.ExpiresAt > DateTime.UtcNow &&
                t.UsedAt == null,
                cancellationToken);

        return entry != null;
    }

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
            return false;

        var normalizedEmail = email.Trim().ToLowerInvariant();
        var tokenHash = ComputeHash(token);

        var entry = await _dbContext.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t =>
                t.TokenHash == tokenHash &&
                t.User.Email.ToLower() == normalizedEmail &&
                t.ExpiresAt > DateTime.UtcNow &&
                t.UsedAt == null,
                cancellationToken);

        if (entry == null) return false;

        var user = entry.User;
        if (!user.IsActive) return false;

        // Hash new password securely
        user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;

        // Invalidate token (single-use)
        entry.UsedAt = DateTime.UtcNow;

        // Record audit trail
        _dbContext.AuditLogs.Add(new AuditLog
        {
            UserId = user.Id,
            Action = "ResetPassword",
            EntityName = nameof(User),
            EntityId = user.Id.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset successfully completed for user [{Email}].", normalizedEmail);
        return true;
    }

    private static string ComputeHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
