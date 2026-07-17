using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

/// <summary>
/// Represents a lead/professor override of a student's submitted contribution.
/// When a lead changes hours or details, an override record is created instead of
/// mutating the original. This creates an immutable audit trail.
/// </summary>
public class ContributionOverride
{
    public int Id { get; set; }

    /// <summary>
    /// Reference to the original student-submitted contribution.
    /// </summary>
    public int ContributionId { get; set; }
    public Contribution? Contribution { get; set; }

    /// <summary>
    /// The user who performed the override (lead, professor, or admin).
    /// </summary>
    public string OverriddenById { get; set; } = string.Empty;
    public User? OverriddenBy { get; set; }

    /// <summary>
    /// When the override was created.
    /// </summary>
    public DateTime OverriddenAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Original hours submitted by student (before override).
    /// </summary>
    public decimal? OriginalHours { get; set; }

    /// <summary>
    /// New hours after override.
    /// </summary>
    public decimal? NewHours { get; set; }

    /// <summary>
    /// Original description submitted by student.
    /// </summary>
    [StringLength(1000)]
    public string? OriginalDescription { get; set; }

    /// <summary>
    /// New description after override (if changed).
    /// </summary>
    [StringLength(1000)]
    public string? NewDescription { get; set; }

    /// <summary>
    /// Justification for the override. Required for transparency.
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Justification { get; set; } = string.Empty;

    /// <summary>
    /// Whether the override is active/approved. Allows for dispute workflow (future).
    /// </summary>
    public bool IsApproved { get; set; } = true;

    /// <summary>
    /// If disputed, reason for dispute (student's response).
    /// </summary>
    [StringLength(500)]
    public string? DisputeReason { get; set; }

    /// <summary>
    /// User who disputed this override (if any).
    /// </summary>
    public string? DisputedById { get; set; }
    public User? DisputedBy { get; set; }

    public DateTime? DisputedAt { get; set; }
}
