namespace TeamSync.Models;

public class RemovalRequest
{
    public int Id { get; set; }
    public int GroupMemberId { get; set; }
    public int GroupId { get; set; }
    public string UserId { get; set; } = string.Empty; // User being removed
    public string RequestedByUserId { get; set; } = string.Empty; // Lead or Student requesting
    public string? ApprovedByUserId { get; set; } // Professor who approved
    public string Reason { get; set; } = string.Empty; // Why they're being removed/leaving
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    // Navigation properties
    public GroupMember? GroupMember { get; set; }
    public Group? Group { get; set; }
    public User? User { get; set; }
    public User? RequestedBy { get; set; }
    public User? ApprovedBy { get; set; }
}
