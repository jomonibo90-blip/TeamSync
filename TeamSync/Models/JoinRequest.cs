namespace TeamSync.Models;

public class JoinRequest
{
    public int Id { get; set; }
    
    public int GroupId { get; set; }
    public Group? Group { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    
    // Default status is Pending for new requests
    public string Status { get; set; } = "Pending";
    
    public string? ApprovedByUserId { get; set; }
    public User? ApprovedBy { get; set; }
    
    // Default creation timestamp
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
