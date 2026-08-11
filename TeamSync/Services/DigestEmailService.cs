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

        // HTML Header with Material 3 Design
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("<style>");
        html.AppendLine("* { margin: 0; padding: 0; box-sizing: border-box; }");
        html.AppendLine("body { font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #181c20; background-color: #f8f9fa; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; background-color: #ffffff; }");
        html.AppendLine(".header { background: linear-gradient(135deg, #0057cd 0%, #0d6efd 100%); color: #ffffff; padding: 40px 30px; border-radius: 12px; margin-bottom: 32px; box-shadow: 0 3px 6px rgba(0, 0, 0, 0.15), 0 2px 4px rgba(0, 0, 0, 0.12); }");
        html.AppendLine(".header h1 { margin: 0 0 12px 0; font-size: 32px; font-weight: 600; font-family: 'Poppins', sans-serif; letter-spacing: -0.5px; }");
        html.AppendLine(".header p { margin: 0; font-size: 14px; opacity: 0.95; }");
        html.AppendLine(".content-section { margin-bottom: 32px; }");
        html.AppendLine(".section-title { font-size: 18px; font-weight: 600; color: #181c20; margin-bottom: 16px; padding-bottom: 8px; border-bottom: 2px solid #e8f0ff; }");
        html.AppendLine(".alert-item { border-left: 4px solid #0057cd; padding: 16px; margin-bottom: 12px; background-color: #f1f4f9; border-radius: 8px; box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12), 0 1px 2px rgba(0, 0, 0, 0.24); transition: all 250ms ease; }");
        html.AppendLine(".alert-item:hover { background-color: #e8f0ff; box-shadow: 0 3px 6px rgba(0, 0, 0, 0.15), 0 2px 4px rgba(0, 0, 0, 0.12); }");
        html.AppendLine(".alert-type { display: inline-block; background-color: #0057cd; color: #ffffff; padding: 6px 12px; border-radius: 6px; font-size: 11px; font-weight: 600; margin-bottom: 8px; letter-spacing: 0.5px; }");
        html.AppendLine(".alert-type.success { background-color: #198754; }");
        html.AppendLine(".alert-type.warning { background-color: #ffc107; color: #181c20; }");
        html.AppendLine(".alert-type.error { background-color: #ba1a1a; }");
        html.AppendLine(".alert-type.info { background-color: #0dcaf0; }");
        html.AppendLine(".alert-message { font-size: 14px; margin: 8px 0; color: #181c20; line-height: 1.5; }");
        html.AppendLine(".alert-time { font-size: 12px; color: #424655; margin-top: 8px; }");
        html.AppendLine(".summary-box { background-color: #e8f5e9; border-left: 4px solid #198754; padding: 16px; border-radius: 8px; margin-bottom: 24px; }");
        html.AppendLine(".summary-box p { color: #1b5e20; margin: 0; font-size: 14px; }");
        html.AppendLine(".footer { border-top: 1px solid #dee2e6; padding-top: 24px; margin-top: 32px; font-size: 12px; color: #424655; text-align: center; }");
        html.AppendLine(".footer a { color: #0057cd; text-decoration: none; font-weight: 500; }");
        html.AppendLine(".footer a:hover { text-decoration: underline; }");
        html.AppendLine(".divider { height: 1px; background-color: #dee2e6; margin: 24px 0; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"container\">");

        // HTML Body
        html.AppendLine("<div class=\"header\">");
        html.AppendLine($"<h1>TeamSync Weekly Digest</h1>");
        html.AppendLine($"<p>Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} – {DateTime.UtcNow:MMM dd, yyyy}</p>");
        html.AppendLine("</div>");

        html.AppendLine($"<p style=\"font-size: 16px; margin-bottom: 16px; color: #181c20;\">Hi <strong>{user.FirstName}</strong>,</p>");
        html.AppendLine($"<p style=\"font-size: 14px; margin-bottom: 20px; color: #424655;\">Here's your weekly summary of alerts and activities from the past week.</p>");

        // Summary Box
        html.AppendLine("<div class=\"summary-box\">");
        html.AppendLine($"<p><strong>📊 Total Alerts:</strong> {alerts.Count} new alerts</p>");
        html.AppendLine("</div>");

        // Plain Text Header
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine("                 TEAMSYNC WEEKLY DIGEST");
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine();
        plainText.AppendLine($"Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} – {DateTime.UtcNow:MMM dd, yyyy}");
        plainText.AppendLine();
        plainText.AppendLine($"Hi {user.FirstName},");
        plainText.AppendLine();
        plainText.AppendLine("Here's your weekly summary of alerts and activities from the past week.");
        plainText.AppendLine();
        plainText.AppendLine($"📊 Total Alerts: {alerts.Count} new alerts");
        plainText.AppendLine();

        // Group alerts by type
        var groupedAlerts = alerts.GroupBy(a => a.Type);
        foreach (var group in groupedAlerts)
        {
            html.AppendLine("<div class=\"content-section\">");
            html.AppendLine($"<h2 class=\"section-title\">{FormatAlertType(group.Key)}</h2>");
            plainText.AppendLine($"{FormatAlertType(group.Key)}");
            plainText.AppendLine(new string('─', 50));

            foreach (var alert in group)
            {
                var alertTypeClass = GetAlertTypeClass(alert.Type);
                html.AppendLine("<div class=\"alert-item\">");
                html.AppendLine($"<div class=\"alert-type {alertTypeClass}\">{FormatAlertType(alert.Type)}</div>");
                html.AppendLine($"<div class=\"alert-message\">{System.Web.HttpUtility.HtmlEncode(alert.Message)}</div>");
                html.AppendLine($"<div class=\"alert-time\">📅 {alert.CreatedAt.ToLocalTime():MMM dd, yyyy 'at' HH:mm}</div>");
                html.AppendLine("</div>");

                plainText.AppendLine($"• {alert.Message}");
                plainText.AppendLine($"  Time: {alert.CreatedAt.ToLocalTime():MMM dd, yyyy HH:mm}");
                plainText.AppendLine();
            }

            html.AppendLine("</div>");
        }

        // Footer
        html.AppendLine("<div class=\"divider\"></div>");
        html.AppendLine("<div class=\"footer\">");
        html.AppendLine("<p style=\"margin-bottom: 12px; color: #424655;\">You're receiving this because you have subscribed to weekly digest emails.</p>");
        html.AppendLine("<p><a href=\"#\">Manage Preferences</a> | <a href=\"#\">Unsubscribe</a></p>");
        html.AppendLine("</div>");

        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        // Plain Text Footer
        plainText.AppendLine();
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine("You're receiving this because you have subscribed to weekly digest emails.");
        plainText.AppendLine("To manage your preferences, visit your account settings in TeamSync.");
        plainText.AppendLine("═══════════════════════════════════════════════════════");

        return (html.ToString(), plainText.ToString());
    }

    private string GetAlertTypeClass(string alertType)
    {
        return alertType switch
        {
            "StatusChange" => "success",
            "ApprovalRejected" => "error",
            "ApprovalRequested" => "warning",
            "Comment" => "info",
            _ => ""
        };
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
