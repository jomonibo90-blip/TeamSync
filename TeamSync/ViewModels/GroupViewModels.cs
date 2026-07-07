using System.ComponentModel.DataAnnotations;

namespace TeamSync.ViewModels;

public class GroupListViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int StudentCount { get; set; } // Count of only students and leads (excluding professors)
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public string UserRole { get; set; } = string.Empty; // e.g., "Lead", "Member", "Professor"
    public DateTime? ArchivedAt { get; set; }
}

public class CreateGroupViewModel
{
    [Required(ErrorMessage = "Group name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Group name must be between 3 and 100 characters")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;
}

public class GroupDetailsViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string JoinCode { get; set; } = string.Empty;
    public string CurrentUserRole { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GroupMemberViewModel> Members { get; set; } = new();
    public List<RemovalRequestViewModel> PendingRemovalRequests { get; set; } = new();
    public List<AddMemberRequestViewModel> PendingAddRequests { get; set; } = new();
    public List<JoinRequestViewModel> PendingJoinRequests { get; set; } = new();

    // Tasks shown on the Group Details page
    public List<TaskListItemViewModel> ActiveTasks { get; set; } = new();
    public List<TaskListItemViewModel> RequestedTasks { get; set; } = new();
}

public class GroupMemberViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
}

public class RemovalRequestViewModel
{
    public int Id { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string RequestedByFullName { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string RequestType { get; set; } = string.Empty; // "Leave" or "Removal"
}

public class AddMemberRequestViewModel
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string RequestedByFullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class JoinRequestViewModel
{
    public int Id { get; set; }
    public string UserFullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class JoinGroupViewModel
{
    [Required(ErrorMessage = "Join Code is required")]
    [StringLength(10, MinimumLength = 6, ErrorMessage = "Invalid code length")]
    [Display(Name = "Join Code")]
    public string JoinCode { get; set; } = string.Empty;
}

public class AddMemberViewModel
{
    [Required]
    public int GroupId { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [Display(Name = "User Email")]
    public string Email { get; set; } = string.Empty;
}

public class EditGroupViewModel
{
    [Required]
    public int Id { get; set; }

    [Required(ErrorMessage = "Group name is required")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Group name must be between 3 and 100 characters")]
    [Display(Name = "Group Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    [Display(Name = "Description")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Active Status")]
    public bool IsActive { get; set; }
}