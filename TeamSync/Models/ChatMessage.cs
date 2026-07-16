namespace TeamSync.Models;

/// <summary>
/// Represents a message sent in a group chat.
/// Messages are group-scoped and only visible to group members.
/// </summary>
public class ChatMessage
{
    public int Id { get; set; }

    /// <summary>
    /// The group this message belongs to.
    /// </summary>
    public int GroupId { get; set; }
    public Group? Group { get; set; }

    /// <summary>
    /// The user who sent this message.
    /// </summary>
    public string SenderId { get; set; } = string.Empty;
    public User? Sender { get; set; }

    /// <summary>
    /// The message content.
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// When the message was created (UTC).
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
