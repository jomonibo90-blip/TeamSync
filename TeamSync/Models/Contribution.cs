using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

public class Contribution
{
    public int Id { get; set; }

    public int TaskId { get; set; }

    // The user the contribution is attributed to (usually the assignee)
    public string UserId { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string Description { get; set; } = string.Empty;

    public DateTime ContributedAt { get; set; } = DateTime.UtcNow;

    // Allow fractional hours (nullable if not provided)
    public decimal? HoursSpent { get; set; }

    // Audit: who recorded this contribution (approver or assignee)
    public string? RecordedById { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    // Source or reason (e.g., "TaskFinalization", "ManualEntry")
    [StringLength(100)]
    public string? Source { get; set; }

    [StringLength(2000)]
    public string? Notes { get; set; }

    /// <summary>
    /// Indicates if this contribution was submitted by the student (vs created by lead/professor).
    /// Student-submitted contributions are immutable to preserve audit trail.
    /// Any changes create separate ContributionOverride records.
    /// </summary>
    public bool IsStudentSubmitted { get; set; } = false;

    // Navigation properties
    public Task? Task { get; set; }
    public User? User { get; set; }
    public User? RecordedBy { get; set; }
    public ICollection<ContributionOverride> Overrides { get; set; } = new List<ContributionOverride>();
}
