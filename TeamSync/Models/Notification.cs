using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    public int? TaskId { get; set; }
    public Task? Task { get; set; }

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = string.Empty; // "DeadlineReminder", "StatusChange", etc.

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    public bool IsRead { get; set; } = false;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
