namespace PharmacySystem.Services.Email;

public class EmailSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string Username { get; set; } = string.Empty;
    private string _password = string.Empty;
    public string Password
    {
        get => _password;
        set => _password = value?.Trim() ?? string.Empty;
    }
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "Pharmacy Management System";
    public bool EnableSsl { get; set; } = true;

    // Backward compatibility aliases if legacy config keys are provided
    public string SmtpServer
    {
        get => Host;
        set { if (string.IsNullOrWhiteSpace(Host)) Host = value; }
    }

    public int SmtpPort
    {
        get => Port;
        set { if (Port == 587 && value != 0) Port = value; }
    }

    public string SenderEmail
    {
        get => FromEmail;
        set { if (string.IsNullOrWhiteSpace(FromEmail)) FromEmail = value; }
    }

    public string SenderName
    {
        get => FromName;
        set { if (string.IsNullOrWhiteSpace(FromName)) FromName = value; }
    }

    public string AppPassword
    {
        get => Password;
        set
        {
            if (string.IsNullOrWhiteSpace(_password) && !string.IsNullOrWhiteSpace(value))
            {
                _password = value.Trim();
            }
        }
    }

    /// <summary>
    /// Checks whether valid, non-placeholder credentials have been configured.
    /// </summary>
    public bool IsConfigured()
    {
        if (string.IsNullOrWhiteSpace(Host) || string.IsNullOrWhiteSpace(FromEmail))
        {
            return false;
        }

        // Check against empty or placeholder values
        var host = Host.Trim().ToLowerInvariant();
        if (host == "localhost" || host == "none" || host == "unconfigured")
        {
            return false;
        }

        // If credentials are required by host (e.g. Gmail or Mailtrap)
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            return false;
        }

        return true;
    }
}

public interface IEmailService
{
    bool IsConfigured();
    Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default);
}
