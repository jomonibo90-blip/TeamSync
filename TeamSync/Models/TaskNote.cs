using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSync.Models;

/// <summary>
/// Represents a discussion note/comment on a task.
/// Allows team members to collaborate and discuss task progress.
/// </summary>
public class TaskNote
{
    public int Id { get; set; }

    [Required]
    public int TaskId { get; set; }
    public Task? Task { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    [Required]
    [StringLength(2000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
