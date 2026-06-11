namespace TeamSync.Models;

public class GroupMember
{
    public int Id { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = "Member"; // Member, Lead, Instructor
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public Group? Group { get; set; }
    public User? User { get; set; }
}
