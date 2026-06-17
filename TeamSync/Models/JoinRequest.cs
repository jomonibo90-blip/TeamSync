namespace TeamSync.Models;

public class JoinRequest
{
    public int Id { get; set; }
    
    public int GroupId { get; set; }
    public Group? Group { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    
    public string Status { get; set; } = string.Empty;
    
    public string? ApprovedByUserId { get; set; }
    public User? ApprovedBy { get; set; }
    
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
