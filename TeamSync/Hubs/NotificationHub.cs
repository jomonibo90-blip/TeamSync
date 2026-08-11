using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSync.Models;
using TeamSync.Data;

namespace TeamSync.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<NotificationHub> _logger;

    public NotificationHub(UserManager<User> userManager, ApplicationDbContext context, ILogger<NotificationHub> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Called when a user connects. Add them to their personal notification group.
    /// </summary>
    public override async System.Threading.Tasks.Task OnConnectedAsync()
    {
        if (Context.User != null)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                // Add this connection to a group named after the user's ID
                // This allows us to send notifications to specific users
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user-{user.Id}");
            }
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Mark a notification as read by the client.
    /// </summary>
    public async System.Threading.Tasks.Task MarkAsRead(int notificationId)
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        var notification = await _context.Notifications.FindAsync(notificationId);
        if (notification != null && notification.UserId == user.Id)
        {
            notification.IsRead = true;
            await _context.SaveChangesAsync();

            // Notify the client that the notification was marked as read
            await Clients.Caller.SendAsync("NotificationMarkedAsRead", notificationId);
        }
    }

    /// <summary>
    /// Request unread notification count for the current user.
    /// </summary>
    public async Task<int> GetUnreadCount()
    {
        try
        {
            if (Context.User == null)
            {
                _logger.LogWarning("GetUnreadCount: Context.User is null");
                return 0;
            }

            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null)
            {
                _logger.LogWarning("GetUnreadCount: User not found for identity: {Identity}", Context.User.Identity?.Name);
                return 0;
            }

            var unreadCount = await _context.Notifications
                .Where(n => n.UserId == user.Id && !n.IsRead)
                .CountAsync();

            _logger.LogInformation("GetUnreadCount: Found {Count} unread notifications for user {UserId}", unreadCount, user.Id);
            return unreadCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUnreadCount");
            throw;
        }
    }

    /// <summary>
    /// Request recent notifications for the current user.
    /// Limited to last 10.
    /// <summary>
    /// Mark all notifications as read for the current user.
    /// </summary>
    public async System.Threading.Tasks.Task MarkAllAsRead()
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .ToListAsync();

        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
        }

        await _context.SaveChangesAsync();

        // Notify the client
        await Clients.Caller.SendAsync("AllNotificationsMarkedAsRead", unreadNotifications.Select(n => n.Id).ToList());
    }

    /// </summary>
    public async Task<List<object>> GetRecentNotifications(int limit = 10)
    {
        if (Context.User == null) return new List<object>();

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return new List<object>();

        var notifications = await _context.Notifications
            .Where(n => n.UserId == user.Id)
            .OrderByDescending(n => n.CreatedAt)
            .Take(limit)
            .Include(n => n.Task)
            .Select(n => new
            {
                n.Id,
                n.Type,
                n.Message,
                n.IsRead,
                n.CreatedAt,
                n.TaskId,
                TaskTitle = n.Task != null ? n.Task.Title : null
            })
            .ToListAsync();

        return notifications.Cast<object>().ToList();
    }
}
