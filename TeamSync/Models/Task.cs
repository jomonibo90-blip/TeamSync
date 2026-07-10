using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSync.Models;

public class Task
{
    public int Id { get; set; }

    public int? GroupId { get; set; }
    public Group? Group { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }

    public string? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public string? CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public DateTime? DueDate { get; set; }

    public int Priority { get; set; } = 2;

    [StringLength(50)]
    public string Status { get; set; } = "Pending";

    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Review workflow fields
    public string? ReviewRequestedById { get; set; }
    public DateTime? ReviewRequestedAt { get; set; }

    // Lead approval (first-step)
    public string? LeadApprovedById { get; set; }
    public DateTime? LeadApprovedAt { get; set; }

    // Final approval by professor/admin
    public string? CompletionApprovedById { get; set; }
    public DateTime? CompletionApprovedAt { get; set; }

    // Navigation properties
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
}
