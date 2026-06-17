namespace TeamSync.Models;

public class AddMemberRequest
{
    public int Id { get; set; }

    public int GroupId { get; set; }
    public Group Group { get; set; }

    public string UserId { get; set; }
    public User User { get; set; }

    public string RequestedByUserId { get; set; }
    public User RequestedBy { get; set; }

    public string Email { get; set; }
    public string Status { get; set; } // "Pending", "Approved", "Rejected"

    public string? ApprovedByUserId { get; set; }
    public User ApprovedBy { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
