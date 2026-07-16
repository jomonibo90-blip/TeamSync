using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSync.Models;
using TeamSync.Data;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Hubs;

/// <summary>
/// SignalR Hub for real-time group chat.
/// Chat is group-scoped: users can only message other members in their group.
/// </summary>
[Authorize]
public class ChatHub : Hub
{
    private readonly UserManager<User> _userManager;
    private readonly ApplicationDbContext _context;

    public ChatHub(UserManager<User> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    /// <summary>
    /// Called when a user connects. Add them to their group's chat group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        if (Context.User != null)
        {
            var user = await _userManager.GetUserAsync(Context.User);
            if (user != null)
            {
                // Get all groups the user is a member of
                var userGroups = await _context.GroupMembers
                    .Where(gm => gm.UserId == user.Id)
                    .Select(gm => gm.GroupId)
                    .ToListAsync();

                // Add connection to each group's chat group
                foreach (var groupId in userGroups)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
                }
            }
        }
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Send a message to a group chat.
    /// Only group members can send messages.
    /// </summary>
    public async Task SendMessage(int groupId, string content)
    {
        if (Context.User == null) return;
        if (string.IsNullOrWhiteSpace(content)) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        // Verify user is a member of this group
        var isMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

        if (!isMember) return;

        // Verify group exists and is active
        var group = await _context.Groups.FindAsync(groupId);
        if (group == null || group.ArchivedAt.HasValue) return;

        // Create and save the message
        var message = new ChatMessage
        {
            GroupId = groupId,
            SenderId = user.Id,
            Content = content.Trim(),
            CreatedAt = DateTime.UtcNow
        };

        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();

        // Broadcast the message to all group members
        await Clients.Group($"group-chat-{groupId}").SendAsync("ReceiveMessage", new
        {
            message.Id,
            message.GroupId,
            SenderName = user.UserName,
            SenderId = user.Id,
            message.Content,
            message.CreatedAt
        });
    }

    /// <summary>
    /// Load recent messages for a group (last N messages).
    /// </summary>
    public async Task LoadHistory(int groupId, int limit = 50)
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        // Verify user is a member of this group
        var isMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

        if (!isMember) return;

        // Get recent messages
        var messages = await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId)
            .OrderByDescending(cm => cm.CreatedAt)
            .Take(limit)
            .Include(cm => cm.Sender)
            .OrderBy(cm => cm.CreatedAt) // Reverse order for display (oldest first in list)
            .Select(cm => new
            {
                cm.Id,
                cm.GroupId,
                SenderName = cm.Sender!.UserName,
                SenderId = cm.SenderId,
                cm.Content,
                cm.CreatedAt
            })
            .ToListAsync();

        // Send history to the caller
        await Clients.Caller.SendAsync("LoadHistoryResponse", messages);
    }

    /// <summary>
    /// Join a group's chat when user visits the group details page.
    /// </summary>
    public async Task JoinGroupChat(int groupId)
    {
        if (Context.User == null) return;

        var user = await _userManager.GetUserAsync(Context.User);
        if (user == null) return;

        // Verify user is a member of this group
        var isMember = await _context.GroupMembers
            .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

        if (!isMember) return;

        await Groups.AddToGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
    }

    /// <summary>
    /// Leave a group's chat when user navigates away.
    /// </summary>
    public async Task LeaveGroupChat(int groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
    }
}
