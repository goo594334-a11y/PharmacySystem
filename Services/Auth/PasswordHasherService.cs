using Microsoft.AspNetCore.Identity;
using PharmacySystem.Models.Entities;

namespace PharmacySystem.Services.Auth;

public interface IPasswordHasherService
{
    string HashPassword(User user, string password);
    bool VerifyPassword(User user, string hashedPassword, string providedPassword);
}

public class PasswordHasherService : IPasswordHasherService
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(User user, string password)
    {
        return _hasher.HashPassword(user, password);
    }

    public bool VerifyPassword(User user, string hashedPassword, string providedPassword)
    {
        if (string.IsNullOrWhiteSpace(hashedPassword) || string.IsNullOrWhiteSpace(providedPassword))
        {
            return false;
        }

        try
        {
            var result = _hasher.VerifyHashedPassword(user, hashedPassword, providedPassword);
            return result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded;
        }
        catch (FormatException)
        {
            // Hash format is invalid - reject login (removed plaintext fallback for security)
            return false;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
