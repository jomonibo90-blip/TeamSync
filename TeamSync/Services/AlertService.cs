using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;

namespace TeamSync.Services;

/// <summary>
/// Service for creating and managing system alerts.
/// Alerts are generated when key events occur (task assignments, approvals, status changes).
/// </summary>
public interface IAlertService
{
    /// <summary>
    /// Create an alert for a specific user.
    /// </summary>
    System.Threading.Tasks.Task<Notification> CreateAlertAsync(string userId, int? taskId, string type, string message);

    /// <summary>
    /// Create an alert for multiple users.
    /// </summary>
    System.Threading.Tasks.Task CreateAlertsAsync(List<string> userIds, int? taskId, string type, string message);

    /// <summary>
    /// Get unread alerts for a user.
    /// </summary>
    System.Threading.Tasks.Task<List<Notification>> GetUnreadAlertsAsync(string userId);

    /// <summary>
    /// Get all alerts for a user (read and unread).
    /// </summary>
    System.Threading.Tasks.Task<List<Notification>> GetAlertsAsync(string userId, int? taskId = null, int limit = 50);

    /// <summary>
    /// Mark an alert as read.
    /// </summary>
    System.Threading.Tasks.Task MarkAsReadAsync(int alertId);

    /// <summary>
    /// Mark all alerts as read for a user.
    /// </summary>
    System.Threading.Tasks.Task MarkAllAsReadAsync(string userId);

    /// <summary>
    /// Get alerts for digest email based on time range.
    /// </summary>
    System.Threading.Tasks.Task<List<Notification>> GetAlertsForDigestAsync(string userId, DateTime startTime, DateTime endTime);

    /// <summary>
    /// Delete old alerts (older than specified days).
    /// </summary>
    System.Threading.Tasks.Task DeleteOldAlertsAsync(int olderThanDays);
}

public class AlertService : IAlertService
{
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly ILogger<AlertService> _logger;

    public AlertService(ApplicationDbContext context, IEmailService emailService, ILogger<AlertService> logger)
    {
        _context = context;
        _emailService = emailService;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task<Notification> CreateAlertAsync(string userId, int? taskId, string type, string message)
    {
        try
        {
            var notification = new Notification
            {
                UserId = userId,
                TaskId = taskId,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            _context.Notifications.Add(notification);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Alert created for user {userId}: {type} - {message}");

            // Send immediate email if user has that preference
            _ = SendImmediateEmailIfEnabledAsync(userId, notification);

            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating alert for user {userId}");
            throw;
        }
    }

    /// <summary>
    /// Send an immediate email if user has "Immediate" notification preference and this alert type is enabled.
    /// This runs async without blocking alert creation.
    /// </summary>
    private async System.Threading.Tasks.Task SendImmediateEmailIfEnabledAsync(string userId, Notification notification)
    {
        try
        {
            _logger.LogInformation($"Starting SendImmediateEmailIfEnabledAsync for user {userId}, alert type: {notification.Type}");

            var user = await _context.Users
                .Include(u => u.AlertPreference)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
            {
                _logger.LogWarning($"Cannot send immediate email: User {userId} not found or has no email");
                return;
            }

            _logger.LogInformation($"User found: {user.Email}, AlertPreference: {user.AlertPreference?.NotificationFrequency}");

            // Check if user has "Immediate" notification preference
            if (user.AlertPreference?.NotificationFrequency != "Immediate")
            {
                _logger.LogInformation($"User {userId} does not have 'Immediate' notification frequency. Frequency: {user.AlertPreference?.NotificationFrequency ?? "NULL"}");
                return;
            }

            // Check if this alert type is enabled in preferences
            var isAlertTypeEnabled = notification.Type switch
            {
                "TaskAssignment" => user.AlertPreference.ReceiveTaskAssignmentAlerts,
                "ApprovalRejection" => user.AlertPreference.ReceiveApprovalRejectionAlerts,
                "StatusChange" => user.AlertPreference.ReceiveStatusChangeAlerts,
                "Comment" => user.AlertPreference.ReceiveCommentAlerts,
                "GroupChange" => user.AlertPreference.ReceiveGroupAlerts,
                _ => false
            };

            _logger.LogInformation($"Alert type {notification.Type} enabled for user {userId}: {isAlertTypeEnabled}");

            if (!isAlertTypeEnabled)
            {
                _logger.LogInformation($"Alert type {notification.Type} is disabled for user {userId}");
                return;
            }

            // Generate and send email
            var subject = $"TeamSync: {notification.Type}";
            var htmlContent = FormatAlertEmailHtml(notification, user.UserName ?? "User");

            _logger.LogInformation($"Sending email to {user.Email} with subject: {subject}");
            await _emailService.SendEmailAsync(user.Email, subject, htmlContent);
            _logger.LogInformation($"Immediate email sent to {user.Email} for alert type {notification.Type}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error sending immediate email for alert to user {userId}");
            // Don't rethrow - we don't want email failures to break alert creation
        }
    }

    public async System.Threading.Tasks.Task CreateAlertsAsync(List<string> userIds, int? taskId, string type, string message)
    {
        try
        {
            var notifications = userIds.Select(userId => new Notification
            {
                UserId = userId,
                TaskId = taskId,
                Type = type,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Alerts created for {userIds.Count} users: {type}");

            // Send immediate emails for each notification if user has that preference
            _logger.LogInformation($"Processing immediate emails for {userIds.Count} users for alert type: {type}");
            foreach (var notification in notifications)
            {
                _logger.LogInformation($"Queuing immediate email for user {notification.UserId}");
                _ = SendImmediateEmailIfEnabledAsync(notification.UserId, notification);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating bulk alerts for {userIds.Count} users");
            throw;
        }
    }

    public async System.Threading.Tasks.Task<List<Notification>> GetUnreadAlertsAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async System.Threading.Tasks.Task<List<Notification>> GetAlertsAsync(string userId, int? taskId = null, int limit = 50)
    {
        var query = _context.Notifications
            .Where(n => n.UserId == userId);

        if (taskId.HasValue)
        {
            query = query.Where(n => n.TaskId == taskId);
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async System.Threading.Tasks.Task MarkAsReadAsync(int alertId)
    {
        var notification = await _context.Notifications.FindAsync(alertId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Alert {alertId} marked as read");
        }
    }

    public async System.Threading.Tasks.Task MarkAllAsReadAsync(string userId)
    {
        var notifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        if (notifications.Count > 0)
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Marked {notifications.Count} alerts as read for user {userId}");
        }
    }

    public async System.Threading.Tasks.Task<List<Notification>> GetAlertsForDigestAsync(string userId, DateTime startTime, DateTime endTime)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && n.CreatedAt >= startTime && n.CreatedAt <= endTime)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
    }

    public async System.Threading.Tasks.Task DeleteOldAlertsAsync(int olderThanDays)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var oldAlerts = await _context.Notifications
            .Where(n => n.CreatedAt < cutoffDate)
            .ToListAsync();

        _context.Notifications.RemoveRange(oldAlerts);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Deleted {oldAlerts.Count} alerts older than {olderThanDays} days");
    }

    /// <summary>
    /// Format a notification alert as HTML email content for immediate sending.
    /// </summary>
    private string FormatAlertEmailHtml(Notification notification, string userName)
    {
        var alertTypeIcon = notification.Type switch
        {
            "TaskAssignment" => "📋",
            "ApprovalRejection" => "⏳",
            "StatusChange" => "🔄",
            "Comment" => "💬",
            "GroupChange" => "👥",
            _ => "🔔"
        };

        var alertTypeLabel = notification.Type switch
        {
            "TaskAssignment" => "Task Assignment",
            "ApprovalRejection" => "Approval/Rejection",
            "StatusChange" => "Status Change",
            "Comment" => "New Comment",
            "GroupChange" => "Group Change",
            _ => "Alert"
        };

        var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: #0d6efd; color: white; padding: 20px; border-radius: 8px 8px 0 0; text-align: center; }}
        .header h1 {{ margin: 0; font-size: 24px; }}
        .content {{ background: #f8f9fa; border: 1px solid #dee2e6; border-top: none; padding: 20px; border-radius: 0 0 8px 8px; }}
        .alert-type {{ color: #0d6efd; font-weight: 600; margin-bottom: 10px; }}
        .message {{ background: white; padding: 15px; border-left: 4px solid #0d6efd; border-radius: 4px; margin: 15px 0; }}
        .timestamp {{ color: #888; font-size: 12px; margin-top: 15px; }}
        .footer {{ text-align: center; color: #888; font-size: 12px; margin-top: 20px; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TeamSync Alert</h1>
        </div>
        <div class='content'>
            <p>Hi {userName},</p>
            <div class='alert-type'>{alertTypeIcon} {alertTypeLabel}</div>
            <div class='message'>
                {notification.Message}
            </div>
            <div class='timestamp'>
                Received at: {notification.CreatedAt:g} UTC
            </div>
            <p>
                <a href='https://localhost/Tasks/Details/{notification.TaskId}' style='color: #0d6efd; text-decoration: none;'>
                    View Details →
                </a>
            </p>
        </div>
        <div class='footer'>
            <p>You're receiving this email because you have Immediate alerts enabled in your alert preferences.</p>
            <p><a href='https://localhost/Account/AlertPreferences' style='color: #0d6efd; text-decoration: none;'>Manage Preferences</a></p>
        </div>
    </div>
</body>
</html>";

        return html;
    }
}
