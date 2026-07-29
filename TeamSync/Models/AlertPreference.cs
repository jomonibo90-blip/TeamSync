using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

/// <summary>
/// Stores user preferences for alerts and email notifications.
/// Users can configure notification frequency and which alert types to receive.
/// </summary>
public class AlertPreference
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }

    /// <summary>
    /// How often the user wants to receive email digests.
    /// Options: "Weekly", "Daily", "Immediate", "Never"
    /// </summary>
    [Required]
    [StringLength(50)]
    public string NotificationFrequency { get; set; } = "Weekly";

    /// <summary>
    /// Day of week to send weekly digest (0=Sunday, 1=Monday, ..., 6=Saturday)
    /// Only used if NotificationFrequency is "Weekly"
    /// </summary>
    public int? DigestDayOfWeek { get; set; } = 1; // Default: Monday

    /// <summary>
    /// Hour of day to send weekly digest (0-23 in UTC)
    /// Only used if NotificationFrequency is "Weekly" or "Daily"
    /// </summary>
    public int? DigestHourUtc { get; set; } = 9; // Default: 9 AM UTC

    /// <summary>
    /// Whether to receive alerts for task assignments
    /// </summary>
    public bool ReceiveTaskAssignmentAlerts { get; set; } = true;

    /// <summary>
    /// Whether to receive alerts for task approvals and rejections
    /// </summary>
    public bool ReceiveApprovalRejectionAlerts { get; set; } = true;

    /// <summary>
    /// Whether to receive alerts for task status changes
    /// </summary>
    public bool ReceiveStatusChangeAlerts { get; set; } = true;

    /// <summary>
    /// Whether to receive alerts for comments/discussions on tasks
    /// </summary>
    public bool ReceiveCommentAlerts { get; set; } = true;

    /// <summary>
    /// Whether to receive alerts for group-related events (member added/removed)
    /// </summary>
    public bool ReceiveGroupAlerts { get; set; } = true;

    /// <summary>
    /// When this preference was created
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this preference was last updated
    /// </summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>
    /// Track when the last digest email was sent to this user
    /// </summary>
    public DateTime? LastDigestSentAt { get; set; }
}
