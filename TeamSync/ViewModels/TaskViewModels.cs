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
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? AssignedToName { get; set; }
        public string? CreatedByName { get; set; }
        public DateTime? DueDate { get; set; }
        public int Priority { get; set; }
    }
}
