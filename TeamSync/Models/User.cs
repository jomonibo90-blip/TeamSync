using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace TeamSync.Models;

public class User : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;

    [Required]
    public string StudentId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation properties
    public ICollection<GroupMember> GroupMembers { get; set; } = new List<GroupMember>();
    public ICollection<Task> CreatedTasks { get; set; } = new List<Task>();
    public ICollection<Contribution> Contributions { get; set; } = new List<Contribution>();
    public ICollection<Group> CreatedGroups { get; set; } = new List<Group>();
    
    /// <summary>
    /// Task assignments for multi-assignee support.
    /// </summary>
    public ICollection<TaskAssignment> TaskAssignments { get; set; } = new List<TaskAssignment>();
    
    /// <summary>
    /// Task notes created by this user.
    /// </summary>
    public ICollection<TaskNote> TaskNotes { get; set; } = new List<TaskNote>();
}
