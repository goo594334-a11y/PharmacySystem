using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace PharmacySystem.Services.Email;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsConfigured() => _settings.IsConfigured();

    public async Task<bool> SendEmailAsync(string toEmail, string subject, string body, bool isHtml = true, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(toEmail))
        {
            _logger.LogWarning("Email dispatch skipped: recipient address is empty.");
            return false;
        }

        if (!_settings.IsConfigured())
        {
            _logger.LogInformation("SMTP service is unconfigured. Development simulated dispatch for recipient [{Recipient}] with Subject [{Subject}].",
                toEmail, subject);
            return false;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.FromName, _settings.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder();
            if (isHtml)
            {
                bodyBuilder.HtmlBody = body;
            }
            else
            {
                bodyBuilder.TextBody = body;
            }

            message.Body = bodyBuilder.ToMessageBody();

            using var client = new SmtpClient();

            // Support STARTTLS or SSL
            var secureSocketOptions = _settings.Port == 465 
                ? SecureSocketOptions.SslOnConnect 
                : SecureSocketOptions.StartTlsWhenAvailable;

            await client.ConnectAsync(_settings.Host, _settings.Port, secureSocketOptions, cancellationToken);

            if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
            {
                await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email successfully dispatched via MailKit to [{Recipient}].", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to [{Recipient}] via SMTP server [{Host}:{Port}].", 
                toEmail, _settings.Host, _settings.Port);
            return false;
        }
    }
}
