namespace TeamSync.ViewModels;

/// <summary>
/// Comprehensive view model for Professor Dashboard with progress tracking, task monitoring, and student activity.
/// </summary>
public class ProfessorDashboardViewModel
{
    // Overall Statistics
    public int ActiveGroupCount { get; set; }
    public int TotalStudentsMonitored { get; set; }
    public int TotalTasksAcrossGroups { get; set; }
    public int CompletedTasksAcrossGroups { get; set; }
    public int InProgressTasksAcrossGroups { get; set; }
    public int PendingTasksAcrossGroups { get; set; }
    public int ReadyForReviewTasksAcrossGroups { get; set; }

    // Task Statistics Summary
    public decimal OverallProgressPercentage => TotalTasksAcrossGroups > 0 
        ? (decimal)CompletedTasksAcrossGroups / TotalTasksAcrossGroups * 100 
        : 0;

    // === TASK ANALYTICS ===
    /// <summary>Count of tasks past their due date</summary>
    public int OverdueTaskCount { get; set; }

    /// <summary>Task completion rate as a percentage (0-100)</summary>
    public decimal TaskCompletionRate =>
        TotalTasksAcrossGroups > 0
            ? (decimal)CompletedTasksAcrossGroups / TotalTasksAcrossGroups * 100
            : 0;

    /// <summary>Count of tasks awaiting work or not started</summary>
    public int PendingTaskCount { get; set; }

    /// <summary>Count of tasks awaiting review/approval</summary>
    public int AwaitingReviewCount { get; set; }

    // === PERFORMANCE INSIGHTS ===
    /// <summary>Top performing groups ranked by completion rate (max 5)</summary>
    public List<TopPerformingGroupViewModel> TopPerformingGroups { get; set; } = new();

    /// <summary>At-risk groups with low completion rates or high overdue counts</summary>
    public List<AtRiskGroupViewModel> AtRiskGroups { get; set; } = new();

    /// <summary>Activity trend data for last 7 days (for chart)</summary>
    public List<ActivityTrendDataPoint> ActivityTrend7Days { get; set; } = new();

    /// <summary>Activity trend data for last 30 days (for chart)</summary>
    public List<ActivityTrendDataPoint> ActivityTrend30Days { get; set; } = new();

    // === STUDENT ENGAGEMENT ===
    /// <summary>Average contributions per monitored student</summary>
    public decimal AverageContributionPerStudent { get; set; }

    /// <summary>Count of students with no contributions in past 7 days</summary>
    public int InactiveStudentCount { get; set; }

    /// <summary>Percentage of students actively contributing (0-100)</summary>
    public decimal StudentSubmissionRate { get; set; }

    /// <summary>List of individual student metrics</summary>
    public List<StudentEngagementMetricViewModel> StudentEngagementMetrics { get; set; } = new();

    // Groups with Enhanced Details
    public List<ProfessorGroupProgressViewModel> Groups { get; set; } = new();

    // Recent Student Activity Feed
    public List<StudentActivityItemViewModel> RecentActivities { get; set; } = new();

    // Pending Approvals
    public List<PendingApprovalViewModel> PendingApprovals { get; set; } = new();
}

/// <summary>
/// Extended group progress view model for professor dashboard with additional detail fields.
/// </summary>
public class ProfessorGroupProgressViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string UserRole { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int StudentCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    // Task Progress
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    public int ReadyForReviewTasks { get; set; }

    // Calculated Progress Percentage
    public decimal ProgressPercentage => TotalTasks > 0 
        ? (decimal)CompletedTasks / TotalTasks * 100 
        : 0;

    // Status for UI display
    public string ProgressStatus => ProgressPercentage switch
    {
        >= 100 => "completed",
        >= 75 => "on-track",
        >= 50 => "in-progress",
        >= 25 => "at-risk",
        _ => "behind"
    };
}

/// <summary>
/// Recent student activity (contributions, submissions, status changes).
/// </summary>
public class StudentActivityItemViewModel
{
    public int Id { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string ActivityType { get; set; } = string.Empty; // "Contribution", "StatusChange", "Submission"
    public string Description { get; set; } = string.Empty;
    public string TaskTitle { get; set; } = string.Empty;
    public DateTime ActivityDate { get; set; }
    public string TimeAgoText => GetTimeAgo(ActivityDate);

    private static string GetTimeAgo(DateTime date)
    {
        var now = DateTime.UtcNow;
        var diff = now - date;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";

        return date.ToString("MMM d");
    }
}

/// <summary>
/// Pending task approvals and actions.
/// </summary>
public class PendingApprovalViewModel
{
    public int TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // "ReadyForReview", "RequestedCompletion"
    public string StudentName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string TimeAgoText => GetTimeAgo(CreatedAt);

    private static string GetTimeAgo(DateTime date)
    {
        var now = DateTime.UtcNow;
        var diff = now - date;

        if (diff.TotalMinutes < 1) return "Just now";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 7) return $"{(int)diff.TotalDays}d ago";

        return date.ToString("MMM d");
    }
}

/// <summary>
/// Top performing group metrics for dashboard ranking.
/// </summary>
public class TopPerformingGroupViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal CompletionRate { get; set; } // 0-100
    public int CompletedTasks { get; set; }
    public int TotalTasks { get; set; }
    public int StudentCount { get; set; }
}

/// <summary>
/// At-risk group metrics for alert/monitoring.
/// </summary>
public class AtRiskGroupViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public decimal CompletionRate { get; set; } // 0-100
    public int OverdueTaskCount { get; set; }
    public int TotalTasks { get; set; }
    public string RiskLevel { get; set; } = string.Empty; // "critical", "high", "medium"
    public string Description { get; set; } = string.Empty; // Why at risk
}

/// <summary>
/// Activity trend data point for charting (7-day and 30-day trends).
/// </summary>
public class ActivityTrendDataPoint
{
    public string Date { get; set; } = string.Empty; // Format: "Mon", "Day 1", etc.
    public int ContributionCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public decimal AverageHoursSpent { get; set; }
}

/// <summary>
/// Individual student engagement metrics.
/// </summary>
public class StudentEngagementMetricViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public int ContributionCount { get; set; }
    public decimal TotalHoursContributed { get; set; }
    public DateTime LastContributedAt { get; set; }
    public bool IsInactive7Days => (DateTime.UtcNow - LastContributedAt).TotalDays >= 7;
    public string ActivityStatus => IsInactive7Days ? "Inactive" : "Active";
}
