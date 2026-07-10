namespace TeamSync.Models;

public class Task
{
    public int Id { get; set; }
    public int? GroupId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedToId { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, In Progress, ReviewRequested, Completed, Rejected, Requested
    public int Priority { get; set; } = 1; // 1=Low, 2=Medium, 3=High
    public DateTime? UpdatedAt { get; set; }

    // Review / completion workflow
    public string? ReviewRequestedById { get; set; }
    public DateTime? ReviewRequestedAt { get; set; }

    public string? CompletionApprovedById { get; set; }
    public DateTime? CompletionApprovedAt { get; set; }

    // Navigation properties
    public Group? Group { get; set; }
    public User? AssignedTo { get; set; }
    public User? CreatedBy { get; set; }
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
}
