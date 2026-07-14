using System.ComponentModel.DataAnnotations;

namespace TeamSync.ViewModels
{
    public class TaskCreateViewModel
    {
        [Required]
        public int GroupId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public string? AssignedToId { get; set; }

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Range(1, 3)]
        public int Priority { get; set; } = 2;
    }

    public class TaskRequestViewModel
    {
        [Required]
        public int GroupId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }
    }

    public class TaskListItemViewModel
    {
        public int Id { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AssignedToId { get; set; }
        public string? AssignedToName { get; set; }
        public string? CreatedById { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; }
        public string? Description { get; set; }

        // Review/workflow
        public string? ReviewRequestedById { get; set; }
        public string? ReviewRequestedByName { get; set; }
        public DateTime? ReviewRequestedAt { get; set; }

        // Lead approval (first step)
        public string? LeadApprovedById { get; set; }
        public string? LeadApprovedByName { get; set; }
        public DateTime? LeadApprovedAt { get; set; }

        // Final approval
        public string? CompletionApprovedById { get; set; }
        public string? CompletionApprovedByName { get; set; }
        public DateTime? CompletionApprovedAt { get; set; }

        // Approval notes
        public string? ApprovalNotes { get; set; }

        // Timestamps
        public DateTime? UpdatedAt { get; set; }

        // UI flags
        public bool CanApprove { get; set; } = false;
        // Role of the current user for this item
        public bool IsLeadForCurrentUser { get; set; } = false;
        public bool IsProfessorForCurrentUser { get; set; } = false;
    }

    public class TaskEditViewModel
    {
        public int Id { get; set; }

        [Required]
        public int GroupId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        public string? AssignedToId { get; set; }

        public List<string> AssignedUserIds { get; set; } = new List<string>();

        public DateTime? StartDate { get; set; }

        public DateTime? DueDate { get; set; }

        [Range(1, 3)]
        public int Priority { get; set; } = 2;
    }

    public class TaskGroupSelectionItemViewModel
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public string? GroupDescription { get; set; }
    }

    public class TaskGroupSelectionViewModel
    {
        public string Mode { get; set; } = "create"; // create | request
        public List<TaskGroupSelectionItemViewModel> Groups { get; set; } = new();
    }
}
