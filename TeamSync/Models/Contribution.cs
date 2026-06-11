namespace TeamSync.Models;

public class Contribution
{
    public int Id { get; set; }
    public int TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime ContributedAt { get; set; } = DateTime.UtcNow;
    public int HoursSpent { get; set; } = 0;

    // Navigation properties
    public Task? Task { get; set; }
    public User? User { get; set; }
}
