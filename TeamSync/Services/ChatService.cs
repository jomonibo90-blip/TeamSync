using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;

namespace TeamSync.Services;

/// <summary>
/// Service for chat message operations.
/// Handles message retrieval, validation, and removal.
/// </summary>
public class ChatService
{
    private readonly ApplicationDbContext _context;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get recent messages for a group.
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetGroupMessagesAsync(int groupId, int limit = 50)
    {
        return await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId)
            .OrderByDescending(cm => cm.CreatedAt)
            .Take(limit)
            .Include(cm => cm.Sender)
            .OrderBy(cm => cm.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get paginated messages for a group.
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetGroupMessagesPagedAsync(
        int groupId,
        int pageNumber = 1,
        int pageSize = 25)
    {
        var skip = (pageNumber - 1) * pageSize;

        return await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId)
            .OrderByDescending(cm => cm.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .Include(cm => cm.Sender)
            .OrderBy(cm => cm.CreatedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Get message count for a group.
    /// </summary>
    public async Task<int> GetGroupMessageCountAsync(int groupId)
    {
        return await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId)
            .CountAsync();
    }

    /// <summary>
    /// Delete a message (typically by the sender or a professor).
    /// </summary>
    public async Task<bool> DeleteMessageAsync(int messageId, string userId)
    {
        var message = await _context.ChatMessages.FindAsync(messageId);
        if (message == null) return false;

        // Only the sender can delete their own message
        if (message.SenderId != userId) return false;

        _context.ChatMessages.Remove(message);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Get messages sent by a specific user in a group.
    /// </summary>
    public async Task<IEnumerable<ChatMessage>> GetUserMessagesInGroupAsync(
        int groupId,
        string userId,
        int limit = 25)
    {
        return await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId && cm.SenderId == userId)
            .OrderByDescending(cm => cm.CreatedAt)
            .Take(limit)
            .Include(cm => cm.Sender)
            .ToListAsync();
    }

    /// <summary>
    /// Get total message count for a user in a group.
    /// </summary>
    public async Task<int> GetUserMessageCountInGroupAsync(int groupId, string userId)
    {
        return await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId && cm.SenderId == userId)
            .CountAsync();
    }

    /// <summary>
    /// Delete all messages in a group (typically when archiving).
    /// </summary>
    public async Task<int> DeleteGroupMessagesAsync(int groupId)
    {
        var messages = await _context.ChatMessages
            .Where(cm => cm.GroupId == groupId)
            .ToListAsync();

        _context.ChatMessages.RemoveRange(messages);
        await _context.SaveChangesAsync();
        return messages.Count;
    }

    /// <summary>
    /// Verify if a user is a member of a group.
    /// </summary>
    public async Task<bool> IsUserGroupMemberAsync(string userId, int groupId)
    {
        return await _context.GroupMembers
            .AnyAsync(gm => gm.UserId == userId && gm.GroupId == groupId);
    }

    /// <summary>
    /// Check if a group exists and is active.
    /// </summary>
    public async Task<bool> IsGroupActiveAsync(int groupId)
    {
        var group = await _context.Groups.FindAsync(groupId);
        return group != null && !group.ArchivedAt.HasValue;
    }
}
