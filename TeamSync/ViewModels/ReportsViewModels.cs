namespace TeamSync.ViewModels;

public class ReportsIndexViewModel
{
    public int TotalGroups { get; set; }
    public int TotalStudents { get; set; }
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public List<GroupReportItemViewModel> Groups { get; set; } = new();
}

public class GroupReportItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int TaskCount { get; set; }
    public int CompletedTaskCount { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class GroupDetailsReportViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int MemberCount { get; set; }
    public List<GroupMemberReportItemViewModel> Members { get; set; } = new();
    public int TaskCount { get; set; }
    public List<TaskReportItemViewModel> Tasks { get; set; } = new();
}

public class GroupMemberReportItemViewModel
{
    public string? Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Email { get; set; }
    public string? Role { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class TaskReportItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
