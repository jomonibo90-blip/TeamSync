namespace TeamSync.Models;

public class Group
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Archived timestamp — null when active
    public DateTime? ArchivedAt { get; set; }

    // Convenience property — indicates archived/read-only
    public bool IsArchived => !IsActive || ArchivedAt != null;

    // Navigation properties
    public User? CreatedBy { get; set; }
    public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
    public ICollection<Task> Tasks { get; set; } = new List<Task>();
}
