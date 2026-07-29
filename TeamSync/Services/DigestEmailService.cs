using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;

namespace TeamSync.Services;

/// <summary>
/// Service for generating and sending weekly digest emails to users.
/// </summary>
public interface IDigestEmailService
{
    /// <summary>
    /// Send weekly digest to a specific user.
    /// </summary>
    System.Threading.Tasks.Task SendUserDigestAsync(string userId);

    /// <summary>
    /// Send weekly digest to all users who have opted in.
    /// </summary>
    System.Threading.Tasks.Task SendAllDigestsAsync();

    /// <summary>
    /// Check if it's time to send digest for a user.
    /// </summary>
    System.Threading.Tasks.Task<bool> ShouldSendDigestAsync(string userId);
}

public class DigestEmailService : IDigestEmailService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<DigestEmailService> _logger;

    public DigestEmailService(ApplicationDbContext context, IEmailService emailService, ILogger<DigestEmailService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task SendUserDigestAsync(string userId)
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.AlertPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning($"User {userId} not found or has no email");
                return;
            }

            // Check if user wants email notifications
            if (user.AlertPreference?.NotificationFrequency == "Never")
            {
                _logger.LogInformation($"User {userId} has opted out of email notifications");
                return;
            }

            // Get alerts from the past week
            var startTime = DateTime.UtcNow.AddDays(-7);
            var alerts = await _context.Notifications
                .Include(n => n.Task)
                .Where(n => n.UserId == userId && n.CreatedAt >= startTime)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            if (!alerts.Any())
            {
                _logger.LogInformation($"No alerts to send for user {userId}");
                return;
            }

            // Filter alerts by user preferences
            var filteredAlerts = FilterAlertsByPreferences(alerts, user.AlertPreference);

            if (!filteredAlerts.Any())
            {
                _logger.LogInformation($"No alerts match preferences for user {userId}");
                return;
            }

            // Generate email content
            var (htmlContent, plainTextContent) = GenerateDigestEmail(user, filteredAlerts);

            // Send email
            await _emailService.SendEmailAsync(user.Email, "TeamSync Weekly Digest", htmlContent, plainTextContent);

            // Update last sent time
            if (user.AlertPreference != null)
            {
                user.AlertPreference.LastDigestSentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"Weekly digest sent to {user.Email} with {filteredAlerts.Count} alerts");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending digest to user {userId}");
        }
    }

    public async System.Threading.Tasks.Task SendAllDigestsAsync()
    {
        try
        {
            var users = await _context.Users
                .Include(u => u.AlertPreference)
                .Where(u => u.AlertPreference != null && u.AlertPreference.NotificationFrequency == "Weekly" && u.IsActive)
                .ToListAsync();

            _logger.LogInformation($"Sending digest emails to {users.Count} users");

            foreach (var user in users)
            {
                if (await ShouldSendDigestAsync(user.Id))
                {
                    await SendUserDigestAsync(user.Id);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending all digest emails");
        }
    }

    public async System.Threading.Tasks.Task<bool> ShouldSendDigestAsync(string userId)
    {
        var user = await _context.Users
            .Include(u => u.AlertPreference)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.AlertPreference == null)
            return false;

        var pref = user.AlertPreference;

        // Check if they have alerts to send
        var startTime = DateTime.UtcNow.AddDays(-7);
        var hasAlerts = await _context.Notifications
            .AnyAsync(n => n.UserId == userId && n.CreatedAt >= startTime);

        if (!hasAlerts)
            return false;

        // Check if it's the right day and time
        if (pref.NotificationFrequency != "Weekly")
            return false;

        if (!pref.DigestDayOfWeek.HasValue || !pref.DigestHourUtc.HasValue)
            return false;

        var now = DateTime.UtcNow;
        var targetDay = (int)now.DayOfWeek;
        var targetHour = now.Hour;

        // Check if today matches the configured day (allowing 1 hour window)
        bool isDayMatch = targetDay == pref.DigestDayOfWeek;
        bool isTimeWindow = targetHour >= pref.DigestHourUtc && targetHour < pref.DigestHourUtc + 1;

        // Check if already sent today
        bool alreadySent = pref.LastDigestSentAt.HasValue && 
                          pref.LastDigestSentAt.Value.Date == now.Date;

        return isDayMatch && isTimeWindow && !alreadySent;
    }

    private List<Notification> FilterAlertsByPreferences(List<Notification> alerts, AlertPreference? preferences)
    {
        if (preferences == null)
            return alerts;

        return alerts.Where(a =>
        {
            return a.Type switch
            {
                "TaskAssignment" => preferences.ReceiveTaskAssignmentAlerts,
                "ApprovalRequested" => preferences.ReceiveApprovalRejectionAlerts,
                "ApprovalRejected" => preferences.ReceiveApprovalRejectionAlerts,
                "StatusChange" => preferences.ReceiveStatusChangeAlerts,
                "Comment" => preferences.ReceiveCommentAlerts,
                "GroupMember" => preferences.ReceiveGroupAlerts,
                _ => true
            };
        }).ToList();
    }

    private (string htmlContent, string plainTextContent) GenerateDigestEmail(User user, List<Notification> alerts)
    {
        var html = new System.Text.StringBuilder();
        var plainText = new System.Text.StringBuilder();

        // HTML Header
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<style>");
        html.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; }");
        html.AppendLine(".header { background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); color: white; padding: 30px; border-radius: 8px; margin-bottom: 30px; }");
        html.AppendLine(".header h1 { margin: 0; font-size: 28px; }");
        html.AppendLine(".alert-item { border-left: 4px solid #667eea; padding: 15px; margin: 15px 0; background: #f8f9fa; border-radius: 4px; }");
        html.AppendLine(".alert-type { display: inline-block; background: #667eea; color: white; padding: 4px 8px; border-radius: 3px; font-size: 12px; font-weight: bold; margin-bottom: 8px; }");
        html.AppendLine(".alert-message { font-size: 14px; margin: 8px 0; }");
        html.AppendLine(".alert-time { font-size: 12px; color: #999; }");
        html.AppendLine(".footer { border-top: 1px solid #ddd; padding-top: 20px; margin-top: 30px; font-size: 12px; color: #666; text-align: center; }");
        html.AppendLine(".footer a { color: #667eea; text-decoration: none; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"container\">");

        // HTML Body
        html.AppendLine("<div class=\"header\">");
        html.AppendLine($"<h1>TeamSync Weekly Digest</h1>");
        html.AppendLine($"<p>Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} - {DateTime.UtcNow:MMM dd, yyyy}</p>");
        html.AppendLine("</div>");

        html.AppendLine($"<p>Hi {user.FirstName},</p>");
        html.AppendLine($"<p>Here's a summary of your alerts from the past week ({alerts.Count} total):</p>");

        // Plain Text Header
        plainText.AppendLine("TEAMSYNC WEEKLY DIGEST");
        plainText.AppendLine(new string('=', 50));
        plainText.AppendLine($"Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} - {DateTime.UtcNow:MMM dd, yyyy}");
        plainText.AppendLine();
        plainText.AppendLine($"Hi {user.FirstName},");
        plainText.AppendLine($"Here's a summary of your alerts from the past week ({alerts.Count} total):");
        plainText.AppendLine();

        // Group alerts by type
        var groupedAlerts = alerts.GroupBy(a => a.Type);
        foreach (var group in groupedAlerts)
        {
            html.AppendLine($"<h3>{FormatAlertType(group.Key)}</h3>");
            plainText.AppendLine($"{FormatAlertType(group.Key)}");
            plainText.AppendLine(new string('-', 30));

            foreach (var alert in group)
            {
                html.AppendLine("<div class=\"alert-item\">");
                html.AppendLine($"<div class=\"alert-type\">{FormatAlertType(alert.Type)}</div>");
                html.AppendLine($"<div class=\"alert-message\">{System.Web.HttpUtility.HtmlEncode(alert.Message)}</div>");
                html.AppendLine($"<div class=\"alert-time\">{alert.CreatedAt.ToLocalTime():MMM dd, yyyy HH:mm}</div>");
                html.AppendLine("</div>");

                plainText.AppendLine($"• {alert.Message}");
                plainText.AppendLine($"  Time: {alert.CreatedAt.ToLocalTime():MMM dd, yyyy HH:mm}");
                plainText.AppendLine();
            }
        }

        // Footer
        html.AppendLine("<div class=\"footer\">");
        html.AppendLine("<p>You can manage your notification preferences in your account settings.</p>");
        html.AppendLine("<p>© 2025 TeamSync. All rights reserved.</p>");
        html.AppendLine("</div>");
        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        plainText.AppendLine();
        plainText.AppendLine("---");
        plainText.AppendLine("You can manage your notification preferences in your account settings.");
        plainText.AppendLine("© 2025 TeamSync. All rights reserved.");

        return (html.ToString(), plainText.ToString());
    }

    private string FormatAlertType(string type)
    {
        return type switch
        {
            "TaskAssignment" => "📋 Task Assignment",
            "ApprovalRequested" => "⏳ Approval Requested",
            "ApprovalRejected" => "❌ Approval Rejected",
            "StatusChange" => "🔄 Status Change",
            "Comment" => "💬 Comments",
            "GroupMember" => "👥 Group Changes",
            _ => type
        };
    }
}
