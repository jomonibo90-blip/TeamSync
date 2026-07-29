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
    private readonly ILogger<AlertService> _logger;

    public AlertService(ApplicationDbContext context, ILogger<AlertService> logger)
    {
        _context = context;
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
            return notification;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating alert for user {userId}");
            throw;
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
}
