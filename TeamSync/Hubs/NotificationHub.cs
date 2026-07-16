using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSync.Models;
using TeamSync.Data;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Hubs;

[Authorize]
public class NotificationHub : Hub
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;

    public NotificationHub(UserManager<User> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    /// <summary>
    /// Called when a user connects. Add them to their personal notification group.
    /// </summary>
    public override async Task OnConnectedAsync()
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
    public async Task MarkAsRead(int notificationId)
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
    public async Task GetUnreadCount()
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        var unreadCount = await _context.Notifications
            .Where(n => n.UserId == user.Id && !n.IsRead)
            .CountAsync();

        await Clients.Caller.SendAsync("UnreadCountUpdated", unreadCount);
    }

    /// <summary>
    /// Request recent notifications for the current user.
    /// Limited to last 10.
    /// </summary>
    public async Task GetRecentNotifications(int limit = 10)
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

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

        await Clients.Caller.SendAsync("LoadRecentNotifications", notifications);
    }
}
