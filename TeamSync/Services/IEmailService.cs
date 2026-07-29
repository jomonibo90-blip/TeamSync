namespace TeamSync.Services;

/// <summary>
/// Interface for sending emails.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Send an email to a single recipient.
    /// </summary>
    Task SendEmailAsync(string toEmail, string subject, string htmlContent, string? plainTextContent = null);

    /// <summary>
    /// Send an email to multiple recipients.
    /// </summary>
    Task SendEmailAsync(List<string> toEmails, string subject, string htmlContent, string? plainTextContent = null);

    /// <summary>
    /// Send an email with attachments.
    /// </summary>
    Task SendEmailWithAttachmentsAsync(string toEmail, string subject, string htmlContent, List<(string filePath, string fileName)> attachments);

    /// <summary>
    /// Check if email is configured and available.
    /// </summary>
    Task<bool> IsConfiguredAsync();
}
