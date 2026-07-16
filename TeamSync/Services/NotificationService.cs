using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Hubs;
using TeamSync.Models;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Services;

public class NotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(
        ApplicationDbContext context,
        IHubContext<NotificationHub> hubContext)
    {
        _context = context;
        _hubContext = hubContext;
    }

    /// <summary>
    /// Create a notification and broadcast it via SignalR in real-time.
    /// </summary>
    public async Task CreateNotificationAsync(
        string userId,
        string type,
        string message,
        int? taskId = null)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Message = message,
            TaskId = taskId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync();

        // Broadcast the new notification to the user via SignalR
        await _hubContext.Clients
            .Group($"user-{userId}")
            .SendAsync("NewNotification", new
            {
                notification.Id,
                notification.Type,
                notification.Message,
                notification.IsRead,
                notification.CreatedAt,
                notification.TaskId,
                TaskTitle = notification.Task?.Title
            });

        // Update unread count
        await UpdateUnreadCountForUser(userId);
    }

    /// <summary>
    /// Create notifications for multiple users at once.
    /// </summary>
    public async Task CreateNotificationsForUsersAsync(
        ICollection<string> userIds,
        string type,
        string message,
        int? taskId = null)
    {
        foreach (var userId in userIds)
        {
            await CreateNotificationAsync(userId, type, message, taskId);
        }
    }

    /// <summary>
    /// Get unread notifications for a user.
    /// </summary>
    public async Task<IEnumerable<Notification>> GetUnreadNotificationsAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Include(n => n.Task)
            .ToListAsync();
    }

    /// <summary>
    /// Get recent notifications for a user (limited to last N).
    /// </summary>
    public async Task<IEnumerable<Notification>> GetRecentNotificationsAsync(
        string userId,
        int limit = 10)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Include(n => n.Task)
            .ToListAsync();
    }

    /// <summary>
    /// Get unread count for a user.
    /// </summary>
    public async Task<int> GetUnreadCountAsync(string userId)
    {
        return await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync();
    }

    /// <summary>
    /// Mark a notification as read.
    /// </summary>
    public async Task MarkAsReadAsync(int notificationId)
    {
        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();

            // Notify the user about the status change
            await _hubContext.Clients
                .Group($"user-{notification.UserId}")
                .SendAsync("NotificationMarkedAsRead", notificationId);

            // Update unread count
            await UpdateUnreadCountForUser(notification.UserId);
        }
    }

    /// <summary>
    /// Mark all notifications as read for a user.
    /// </summary>
    public async Task MarkAllAsReadAsync(string userId)
    {
        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        // Notify the user
        await _hubContext.Clients
            .Group($"user-{userId}")
            .SendAsync("AllNotificationsMarkedAsRead", unreadNotifications.Select(n => n.Id));

        // Update unread count
        await UpdateUnreadCountForUser(userId);
    }

    /// <summary>
    /// Delete old notifications (older than specified days).
    /// </summary>
    public async Task DeleteOldNotificationsAsync(int olderThanDays = 30)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-olderThanDays);
        var oldNotifications = _context.Notifications
            .Where(n => n.CreatedAt < cutoffDate && n.IsRead);

        await oldNotifications.ExecuteDeleteAsync();
    }

    /// <summary>
    /// Helper to broadcast unread count to a user.
    /// </summary>
    private async Task UpdateUnreadCountForUser(string userId)
    {
        var unreadCount = await GetUnreadCountAsync(userId);
        await _hubContext.Clients
            .Group($"user-{userId}")
            .SendAsync("UnreadCountUpdated", unreadCount);
    }

    /// <summary>
    /// Check for duplicate notifications to avoid alert spam.
    /// </summary>
    public async Task<bool> HasRecentNotificationAsync(
        string userId,
        string type,
        int? taskId,
        int withinLastMinutes = 5)
    {
        var cutoffTime = DateTime.UtcNow.AddMinutes(-withinLastMinutes);
        return await _context.Notifications
            .AnyAsync(n =>
                n.UserId == userId &&
                n.Type == type &&
                n.TaskId == taskId &&
                n.CreatedAt > cutoffTime);
    }
}
