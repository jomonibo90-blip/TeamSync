using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Options;

namespace TeamSync.Services;

/// <summary>
/// Email configuration settings.
/// </summary>
public class EmailSettings
{
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public string SmtpUsername { get; set; } = string.Empty;
    public string SmtpPassword { get; set; } = string.Empty;
    public string FromEmail { get; set; } = string.Empty;
    public string FromName { get; set; } = "TeamSync";
    public bool UseSSL { get; set; } = true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(FromEmail);
}

/// <summary>
/// Email service implementation using SMTP.
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string htmlContent, string? plainTextContent = null)
    {
        await SendEmailAsync(new List<string> { toEmail }, subject, htmlContent, plainTextContent);
    }

    public async Task SendEmailAsync(List<string> toEmails, string subject, string htmlContent, string? plainTextContent = null)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("Email service is not configured. Email not sent.");
            return;
        }

        try
        {
            using (var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort))
            {
                client.EnableSsl = _settings.UseSSL;
                client.Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword);

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(_settings.FromEmail, _settings.FromName);

                    foreach (var email in toEmails)
                    {
                        mailMessage.To.Add(new MailAddress(email));
                    }

                    mailMessage.Subject = subject;

                    // When we have both HTML and plain text, use only AlternateViews (multipart/alternative)
                    if (!string.IsNullOrWhiteSpace(plainTextContent))
                    {
                        // Clear the body and set up as multipart
                        mailMessage.Body = "";

                        // Create views - HTML first (will be preferred by most clients)
                        var htmlView = AlternateView.CreateAlternateViewFromString(htmlContent, Encoding.UTF8, "text/html");
                        var plainView = AlternateView.CreateAlternateViewFromString(plainTextContent, Encoding.UTF8, "text/plain");

                        mailMessage.AlternateViews.Add(plainView);
                        mailMessage.AlternateViews.Add(htmlView);
                        mailMessage.IsBodyHtml = false; // Let the AlternateViews handle it
                    }
                    else
                    {
                        // Only HTML content
                        mailMessage.Body = htmlContent;
                        mailMessage.IsBodyHtml = true;
                    }

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email sent to {toEmails.Count} recipient(s): {subject}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending email to {string.Join(", ", toEmails)}: {subject}");
            throw;
        }
    }

    public async Task SendEmailWithAttachmentsAsync(string toEmail, string subject, string htmlContent, List<(string filePath, string fileName)> attachments)
    {
        if (!_settings.IsConfigured)
        {
            _logger.LogWarning("Email service is not configured. Email not sent.");
            return;
        }

        try
        {
            using (var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort))
            {
                client.EnableSsl = _settings.UseSSL;
                client.Credentials = new NetworkCredential(_settings.SmtpUsername, _settings.SmtpPassword);

                using (var mailMessage = new MailMessage())
                {
                    mailMessage.From = new MailAddress(_settings.FromEmail, _settings.FromName);
                    mailMessage.To.Add(new MailAddress(toEmail));
                    mailMessage.Subject = subject;
                    mailMessage.Body = htmlContent;
                    mailMessage.IsBodyHtml = true;

                    foreach (var (filePath, fileName) in attachments)
                    {
                        if (File.Exists(filePath))
                        {
                            mailMessage.Attachments.Add(new Attachment(filePath, fileName));
                        }
                    }

                    await client.SendMailAsync(mailMessage);
                    _logger.LogInformation($"Email with {attachments.Count} attachment(s) sent to {toEmail}: {subject}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending email with attachments to {toEmail}: {subject}");
            throw;
        }
    }

    public async Task<bool> IsConfiguredAsync()
    {
        return await Task.FromResult(_settings.IsConfigured);
    }
}
