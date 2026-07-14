using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSync.Models;

/// <summary>
/// Represents an assignment of a task to a user.
/// Allows multi-user assignment to a single task.
/// </summary>
public class TaskAssignment
{
    public int Id { get; set; }

    [Required]
    public int TaskId { get; set; }
    public Task? Task { get; set; }

    [Required]
    public string AssignedToId { get; set; } = string.Empty;
    public User? AssignedTo { get; set; }

    [Required]
    public string AssignedByUserId { get; set; } = string.Empty;
    public User? AssignedByUser { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime? RemovedAt { get; set; }
}
