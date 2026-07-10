using System.ComponentModel.DataAnnotations;

namespace TeamSync.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalUsers { get; set; }
    public int TotalProfessors { get; set; }
    public int TotalStudents { get; set; }
    public int TotalGroups { get; set; }
    public int ActiveGroups { get; set; }
}

public class UserListViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int GroupCount { get; set; }
}

public class EnrollUserViewModel
{
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "First name must be between 2 and 100 characters")]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Last name must be between 2 and 100 characters")]
    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Student ID is required")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Student ID must be between 3 and 50 characters")]
    [Display(Name = "Student ID")]
    public string StudentId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters long")]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required")]
    [Display(Name = "User Role")]
    public string Role { get; set; } = "Student"; // Student, Professor, Admin
}

public class ManageUserViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<UserGroupViewModel> Groups { get; set; } = new();
}

public class UserGroupViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class StudentDashboardViewModel
{
    public List<GroupListViewModel> Groups { get; set; } = new();
    public StudentProgressViewModel Progress { get; set; } = new();
}

public class StudentProgressViewModel
{
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    
    public decimal CompletionPercentage => TotalTasks > 0 
        ? (CompletedTasks * 100m) / TotalTasks 
        : 0;
    
    public Dictionary<int, GroupProgressViewModel> GroupProgress { get; set; } = new();
}

public class GroupProgressViewModel
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Completed { get; set; }
    public int InProgress { get; set; }
    public int Pending { get; set; }
    
    public decimal Percentage => Total > 0 
        ? (Completed * 100m) / Total 
        : 0;
}
