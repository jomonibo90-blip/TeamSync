using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

public class ContributionHistory
{
    public int Id { get; set; }

    // FK to Contribution (may be null if contribution deleted and removed)
    public int ContributionId { get; set; }

    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty; // Created | Updated | Deleted

    public string PerformedById { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    // Free-text description of what changed (JSON or human readable)
    [StringLength(4000)]
    public string? Changes { get; set; }

    // Navigation (optional)
    public Contribution? Contribution { get; set; }
}
