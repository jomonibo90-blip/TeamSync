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
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(UserManager<User> userManager, ApplicationDbContext context, ILogger<ChatHub> logger)
    {
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Called when a user connects. Add them to their group's chat group.
    /// </summary>
    public override async Task OnConnectedAsync()
    {
        try
        {
            _logger.LogInformation("Chat connection attempt from user: {User}", Context.User?.Identity?.Name ?? "Unknown");

            if (Context.User != null)
            {
                var user = await _userManager.GetUserAsync(Context.User);
                if (user != null)
                {
                    _logger.LogInformation("User {UserId} connected to chat", user.Id);

                    // Get all groups the user is a member of
                    var userGroups = await _context.GroupMembers
                        .Where(gm => gm.UserId == user.Id)
                        .Select(gm => gm.GroupId)
                        .ToListAsync();

                    _logger.LogInformation("User {UserId} is member of {GroupCount} groups", user.Id, userGroups.Count);

                    // Add connection to each group's chat group
                    foreach (var groupId in userGroups)
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
                        _logger.LogInformation("User {UserId} added to group-chat-{GroupId}", user.Id, groupId);
                    }
                }
                else
                {
                    _logger.LogWarning("User not found for identity: {Identity}", Context.User.Identity?.Name ?? "Unknown");
                }
            }
            else
            {
                _logger.LogWarning("Context.User is null");
            }

            await base.OnConnectedAsync();
            _logger.LogInformation("Chat connection completed for {ConnectionId}", Context.ConnectionId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ChatHub.OnConnectedAsync");
            throw;
        }
    }

    /// <summary>
    /// Send a message to a group chat.
    /// Only group members can send messages.
    /// </summary>
    public async Task SendMessage(int groupId, string content)
    {
        try
        {
            if (Context.User == null) 
            {
                _logger.LogWarning("SendMessage: Context.User is null");
                return;
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("SendMessage: Content is empty");
                return;
            }

            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null)
            {
                _logger.LogWarning("SendMessage: User not found");
                return;
            }

            // Verify user is a member of this group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember)
            {
                _logger.LogWarning("SendMessage: User {UserId} is not a member of group {GroupId}", user.Id, groupId);
                return;
            }

            // Verify group exists and is active
            var group = await _context.Groups.FindAsync(groupId);
            if (group == null || group.ArchivedAt.HasValue)
            {
                _logger.LogWarning("SendMessage: Group {GroupId} not found or archived", groupId);
                return;
            }

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

            _logger.LogInformation("Message saved: {MessageId} from {UserId} to group {GroupId}", message.Id, user.Id, groupId);

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

            _logger.LogInformation("Message broadcast to group-chat-{GroupId}", groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SendMessage for group {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Load recent messages for a group (last N messages).
    /// </summary>
    public async Task LoadHistory(int groupId, int limit = 50)
    {
        try
        {
            if (Context.User == null)
            {
                _logger.LogWarning("LoadHistory: Context.User is null");
                return;
            }

            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null)
            {
                _logger.LogWarning("LoadHistory: User not found");
                return;
            }

            // Verify user is a member of this group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember)
            {
                _logger.LogWarning("LoadHistory: User {UserId} is not a member of group {GroupId}", user.Id, groupId);
                return;
            }

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

            _logger.LogInformation("LoadHistory: Loaded {MessageCount} messages for group {GroupId}", messages.Count, groupId);

            // Send history to the caller
            await Clients.Caller.SendAsync("LoadHistoryResponse", messages);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in LoadHistory for group {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Join a group's chat when user visits the group details page.
    /// </summary>
    public async Task JoinGroupChat(int groupId)
    {
        try
        {
            if (Context.User == null)
            {
                _logger.LogWarning("JoinGroupChat: Context.User is null");
                return;
            }

            var user = await _userManager.GetUserAsync(Context.User);
            if (user == null)
            {
                _logger.LogWarning("JoinGroupChat: User not found");
                return;
            }

            // Verify user is a member of this group
            var isMember = await _context.GroupMembers
                .AnyAsync(gm => gm.GroupId == groupId && gm.UserId == user.Id);

            if (!isMember)
            {
                _logger.LogWarning("JoinGroupChat: User {UserId} is not a member of group {GroupId}", user.Id, groupId);
                return;
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
            _logger.LogInformation("JoinGroupChat: User {UserId} joined group-chat-{GroupId}", user.Id, groupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in JoinGroupChat for group {GroupId}", groupId);
            throw;
        }
    }

    /// <summary>
    /// Leave a group's chat when user navigates away.
    /// </summary>
    public async Task LeaveGroupChat(int groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-chat-{groupId}");
    }
}
