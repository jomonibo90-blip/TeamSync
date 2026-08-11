using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Azure;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.Services;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TasksController> _logger;
    private readonly NotificationService _notificationService;
    private readonly IAlertService _alertService;
    private readonly IConfiguration _configuration;
    private readonly IBlobStorageService _blobStorageService;

    public TasksController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<TasksController> logger,
        NotificationService notificationService,
        IAlertService alertService,
        IConfiguration configuration,
        IBlobStorageService blobStorageService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _notificationService = notificationService;
        _alertService = alertService;
        _configuration = configuration;
        _blobStorageService = blobStorageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status, bool? archived)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLeadInAnyGroup = await _context.GroupMembers
            .AnyAsync(gm => gm.UserId == currentUser.Id && gm.Role == "Lead" && gm.Group != null && gm.Group.IsActive);
        bool isMemberInAnyGroup = await _context.GroupMembers
            .AnyAsync(gm => gm.UserId == currentUser.Id && gm.Role == "Member" && gm.Group != null && gm.Group.IsActive);

        var query = _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .AsQueryable();

        // Filter by archive status
        if (archived == true)
        {
            query = query.Where(t => t.ArchivedAt.HasValue);
        }
        else
        {
            query = query.Where(t => !t.ArchivedAt.HasValue);  // Active tasks by default
        }

        if (!isAdmin && !isProfessor)
        {
            // Non-admin/professor: show tasks assigned to the user or in groups the user is a member of
            var groupIds = await _context.GroupMembers
                .Where(gm => gm.UserId == currentUser.Id)
                .Select(gm => gm.GroupId)
                .Distinct()
                .ToListAsync();

            query = query.Where(t => t.AssignedToId == currentUser.Id || (t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value)));
        }

        var activeStatus = string.IsNullOrWhiteSpace(status) ? "All" : status.Trim();
        if (!activeStatus.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(t => t.Status == activeStatus);
        }

        var tasks = await query.OrderByDescending(t => t.CreatedAt).ToListAsync();

        var vm = tasks.Select(t =>
        {
            var isLeadForThis = t.Group != null && t.Group.Members.Any(m => m.UserId == currentUser.Id && m.Role == "Lead");
            var isProfessorForCurrentUser = isProfessor || User.IsInRole("Admin");

            return new global::TeamSync.ViewModels.TaskListItemViewModel
            {
                Id = t.Id,
                GroupId = t.GroupId,
                GroupName = t.Group != null ? t.Group.Name : null,
                Title = t.Title,
                Status = t.Status,
                AssignedToId = t.AssignedToId,
                AssignedToName = t.AssignedTo != null ? $"{t.AssignedTo.FirstName} {t.AssignedTo.LastName}" : null,
                CreatedById = t.CreatedById,
                CreatedByName = t.CreatedBy != null ? $"{t.CreatedBy.FirstName} {t.CreatedBy.LastName}" : null,
                StartDate = t.StartDate,
                DueDate = t.DueDate,
                Priority = t.Priority,
                Description = t.Description,

                // workflow fields for UI
                ReviewRequestedById = t.ReviewRequestedById,
                ReviewRequestedAt = t.ReviewRequestedAt,
                LeadApprovedById = t.LeadApprovedById,
                LeadApprovedAt = t.LeadApprovedAt,
                CompletionApprovedById = t.CompletionApprovedById,
                CompletionApprovedAt = t.CompletionApprovedAt,
                ApprovalNotes = t.ApprovalNotes,

                UpdatedAt = t.UpdatedAt,

                // compute CanApprove for current user
                CanApprove = isAdmin || isProfessor || isLeadForThis,
                IsLeadForCurrentUser = isLeadForThis,
                IsProfessorForCurrentUser = isProfessorForCurrentUser
            };
        }).ToList();

        var canCreateTask = isAdmin || isProfessor || isLeadInAnyGroup;
        ViewBag.ActiveStatus = activeStatus;
        ViewBag.IsArchivedView = archived ?? false;
        ViewBag.CanCreateTask = canCreateTask;
        ViewBag.CanRequestTask = !canCreateTask && isMemberInAnyGroup;
        ViewBag.CurrentUserId = currentUser.Id; // used by view to show card-level actions

        // Load assignee counts for each task
        var taskIds = vm.Select(t => t.Id).ToList();
        var assignmentCounts = await _context.TaskAssignments
            .Where(ta => taskIds.Contains(ta.TaskId) && ta.RemovedAt == null)
            .GroupBy(ta => ta.TaskId)
            .Select(g => new { TaskId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.TaskId, x => x.Count);

        ViewBag.TaskAssignments = assignmentCounts;

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> SelectGroup(string? mode)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Determine action: "create" or "request"
        var actionMode = mode ?? "create";

        // Get groups the user is a member of (filter by active groups only)
        var groups = await _context.GroupMembers
            .Where(gm => gm.UserId == currentUser.Id && gm.Group != null && gm.Group.IsActive)
            .Select(gm => new TaskGroupSelectionItemViewModel
            {
                GroupId = gm.GroupId,
                GroupName = gm.Group.Name,
                GroupDescription = gm.Group.Description
            })
            .OrderBy(g => g.GroupName)
            .ToListAsync();

        var vm = new TaskGroupSelectionViewModel
        {
            Mode = actionMode,
            Groups = groups
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Create(int groupId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (groupId <= 0)
        {
            _logger.LogWarning("TasksController.Create called with invalid groupId: {GroupId} by user {UserId}", groupId, currentUser?.Id);
            return BadRequest("Invalid group selected.");
        }

        var group = await _context.Groups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return NotFound();
        if (!group.IsActive) return BadRequest("Cannot add tasks to an archived group.");

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        ViewBag.Members = group.Members.Select(m => m.User).Where(u => u != null).ToList();
        var vm = new TaskCreateViewModel { GroupId = groupId, StartDate = DateTime.UtcNow.Date, DueDate = DateTime.UtcNow.AddDays(7) };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TaskCreateViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Hard validation: group id must be present and > 0
        if (model.GroupId <= 0)
        {
            _logger.LogWarning("Attempt to create task with invalid GroupId {GroupId} by user {UserId}", model.GroupId, currentUser?.Id);
            ModelState.AddModelError("GroupId", "Please select a valid project/group.");
            ViewBag.Members = new List<User>();
            return View(model);
        }

        // Load group and members first so we can repopulate the select if we need to redisplay form
        var group = await _context.Groups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == model.GroupId);
        if (group == null)
        {
            _logger.LogWarning("Create called with non-existing GroupId {GroupId} by user {UserId}", model.GroupId, currentUser?.Id);
            return NotFound();
        }
        if (!group.IsActive) return BadRequest("Cannot add tasks to an archived group.");

        // If model state invalid, ensure ViewBag.Members is populated before returning the view
        if (!ModelState.IsValid)
        {
            ViewBag.Members = group.Members.Select(m => m.User).OfType<Models.User>().ToList();
            return View(model);
        }

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLeadLocal = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLeadLocal)
            return Forbid();

        if (!string.IsNullOrEmpty(model.AssignedToId))
        {
            var assignedIsMember = group.Members.Any(m => m.UserId == model.AssignedToId);
            if (!assignedIsMember)
            {
                ModelState.AddModelError("AssignedToId", "Assigned user must be a member of the group.");
                ViewBag.Members = group.Members.Select(m => m.User).ToList();
                return View(model);
            }

            // Prevent assigning tasks to professors
            var assignedUser = group.Members.FirstOrDefault(m => m.UserId == model.AssignedToId)?.User;
            if (assignedUser != null && await _userManager.IsInRoleAsync(assignedUser, "Professor"))
            {
                ModelState.AddModelError("AssignedToId", "Tasks cannot be assigned to professors.");
                ViewBag.Members = group.Members.Select(m => m.User).ToList();
                return View(model);
            }
        }

        var task = new global::TeamSync.Models.Task
        {
            Title = model.Title,
            Description = model.Description,
            GroupId = model.GroupId,
            AssignedToId = model.AssignedToId,
            CreatedById = currentUser.Id,
            StartDate = model.StartDate?.Date,
            DueDate = model.DueDate,
            Priority = model.Priority,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Task created successfully.";
        return RedirectToAction("Details", "Groups", new { id = model.GroupId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot edit tasks for an archived or missing group.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLead = currentMember?.Role == "Lead";
        bool isCreator = task.CreatedById == currentUser.Id;

        // Allow creator, lead, professor, or admin to edit
        if (!isAdmin && !isProfessor && !isLead && !isCreator)
            return Forbid();

        var vm = new TaskEditViewModel
        {
            Id = task.Id,
            GroupId = task.GroupId ?? 0,
            Title = task.Title,
            Description = task.Description,
            StartDate = task.StartDate,
            DueDate = task.DueDate,
            Priority = task.Priority
        };

        // Members for multi-select
        ViewBag.Members = task.Group.Members.Select(m => m.User).Where(u => u != null).ToList();

        // Current assigned user ids (active assignments) + single AssignedToId
        var assignedIds = task.Assignments?.Where(a => a.RemovedAt == null).Select(a => a.AssignedToId).ToList() ?? new List<string>();
        if (!string.IsNullOrEmpty(task.AssignedToId) && !assignedIds.Contains(task.AssignedToId))
            assignedIds.Add(task.AssignedToId);

        ViewBag.AssignedUserIds = assignedIds;

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(TaskEditViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!ModelState.IsValid)
        {
            // reload members for the view
            var grp = await _context.Groups.Include(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(g => g.Id == model.GroupId);
            ViewBag.Members = grp?.Members.Select(m => m.User).OfType<Models.User>().ToList() ?? new List<Models.User>();
            ViewBag.AssignedUserIds = model.AssignedUserIds ?? new List<string>();
            return View(model);
        }

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .Include(t => t.Assignments)
            .FirstOrDefaultAsync(t => t.Id == model.Id);

        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot edit tasks for an archived or missing group.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLead = currentMember?.Role == "Lead";
        bool isCreator = task.CreatedById == currentUser.Id;

        if (!isAdmin && !isProfessor && !isLead && !isCreator)
            return Forbid();

        // Validate selected assignees are group members
        var selectedIds = model.AssignedUserIds ?? new List<string>();
        var groupMemberIds = task.Group.Members.Select(m => m.UserId).ToHashSet();
        foreach (var id in selectedIds)
        {
            if (!groupMemberIds.Contains(id))
            {
                ModelState.AddModelError("AssignedUserIds", "One or more selected users are not members of the group.");
            }
        }

        // Prevent assigning tasks to professors
        foreach (var id in selectedIds)
        {
            var member = task.Group.Members.FirstOrDefault(m => m.UserId == id);
            if (member?.User != null && await _userManager.IsInRoleAsync(member.User, "Professor"))
            {
                ModelState.AddModelError("AssignedUserIds", "Tasks cannot be assigned to professors.");
                break;
            }
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Members = task.Group.Members.Select(m => m.User).OfType<Models.User>().ToList();
            ViewBag.AssignedUserIds = selectedIds;
            return View(model);
        }

        // Update task fields
        task.Title = model.Title;
        task.Description = model.Description;
        task.StartDate = model.StartDate?.Date;
        task.DueDate = model.DueDate;
        task.Priority = model.Priority;
        task.UpdatedAt = DateTime.UtcNow;

        // Manage assignments: mark removed those not selected, add new for those selected
        var existing = task.Assignments?.Where(a => a.RemovedAt == null).ToList() ?? new List<TaskAssignment>();
        var existingIds = existing.Select(a => a.AssignedToId).ToHashSet();

        // Add new assignments
        foreach (var id in selectedIds.Where(s => !existingIds.Contains(s)))
        {
            var assignment = new TaskAssignment { TaskId = task.Id, AssignedToId = id, AssignedByUserId = currentUser.Id, AssignedAt = DateTime.UtcNow };
            _context.TaskAssignments.Add(assignment);
        }

        // Remove assignments that were unselected
        foreach (var a in existing.Where(a => !selectedIds.Contains(a.AssignedToId)))
        {
            a.RemovedAt = DateTime.UtcNow;
            _context.TaskAssignments.Update(a);
        }

        // Update single AssignedToId to first selected or null
        task.AssignedToId = selectedIds.FirstOrDefault();

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Notify assigned users if assignment changed
        var newlyAssignedIds = selectedIds.Where(id => !existingIds.Contains(id)).ToList();
        if (newlyAssignedIds.Any())
        {
            var message = $"You have been assigned to task: {task.Title}";
            await _notificationService.CreateNotificationsForUsersAsync(
                newlyAssignedIds,
                "StatusChange",
                message,
                task.Id);

            // Create alerts for newly assigned users
            await _alertService.CreateAlertsAsync(
                newlyAssignedIds,
                task.Id,
                "TaskAssignment",
                message);
        }

        TempData["SuccessMessage"] = "Task updated successfully.";

        return RedirectToAction("Details", new { id = task.Id });
    }

    // Student task request flow - renamed to avoid hiding ControllerBase.Request
    [HttpGet]
    public async Task<IActionResult> RequestTask(int groupId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);
        if (group == null) return NotFound();
        if (!group.IsActive) return BadRequest("Cannot request tasks for an archived group.");

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        if (currentMember == null) return Forbid();

        // Only allow regular members (students) to request tasks
        bool isStudent = currentMember.Role == "Member";
        if (!isStudent) return Forbid();

        var vm = new TaskRequestViewModel { GroupId = groupId };
        return View("Request", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestTask(TaskRequestViewModel model)
    {
        if (!ModelState.IsValid) return View("Request", model);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == model.GroupId);
        if (group == null) return NotFound();
        if (!group.IsActive) return BadRequest("Cannot request tasks for an archived group.");

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        if (currentMember == null) return Forbid();

        // Only allow regular members (students) to request tasks
        bool isStudent = currentMember.Role == "Member";
        if (!isStudent) return Forbid();

        var task = new global::TeamSync.Models.Task
        {
            Title = model.Title,
            Description = model.Description,
            GroupId = model.GroupId,
            CreatedById = currentUser.Id,
            Status = "Requested",
            CreatedAt = DateTime.UtcNow
        };

        _context.Tasks.Add(task);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Task request submitted. A professor or lead will review it.";
        return RedirectToAction("Details", "Groups", new { id = model.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRequest(int taskId, DateTime? dueDate, DateTime? startDate, string? assignedToId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive) return BadRequest("Cannot modify tasks for an archived or missing group.");

        var group = task.Group;
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdminLocal = User.IsInRole("Admin");
        bool isProfessorLocal = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLeadLocal = currentMember?.Role == "Lead";

        if (!isAdminLocal && !isProfessorLocal && !isLeadLocal) return Forbid();

        if (task.Status != "Requested")
        {
            TempData["ErrorMessage"] = "Task is not in requested state.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        var todayUtc = DateTime.UtcNow.Date;
        if (dueDate.HasValue && dueDate.Value.Date < todayUtc)
        {
            TempData["ErrorMessage"] = "Due date cannot be in the past.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        if (startDate.HasValue && startDate.Value.Date < todayUtc)
        {
            TempData["ErrorMessage"] = "Start date cannot be in the past.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        if (dueDate.HasValue && startDate.HasValue && startDate.Value.Date > dueDate.Value.Date)
        {
            TempData["ErrorMessage"] = "Start date cannot be after due date.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        // If approver explicitly provided an assignee, validate membership
        if (!string.IsNullOrWhiteSpace(assignedToId))
        {
            var assignedIsMemberExplicit = group.Members.Any(m => m.UserId == assignedToId);
            if (!assignedIsMemberExplicit)
            {
                TempData["ErrorMessage"] = "Assigned user must be an active member of the group.";
                return RedirectToAction("Details", "Groups", new { id = task.GroupId });
            }

            task.AssignedToId = assignedToId;
        }
        else
        {
            // No explicit assignee: default to requester if still a member, otherwise leave unassigned and warn
            var requesterId = task.CreatedById;
            var requesterIsMember = !string.IsNullOrEmpty(requesterId) && group.Members.Any(m => m.UserId == requesterId);
            if (requesterIsMember)
            {
                task.AssignedToId = requesterId;
            }
            else
            {
                // leave unassigned but surface a warning so approver knows requester left
                task.AssignedToId = null;
                TempData["WarningMessage"] = "Requester is no longer a member. Task approved but left unassigned — please assign a member.";
            }
        }

        task.StartDate = startDate?.Date ?? todayUtc;
        task.DueDate = (dueDate?.Date ?? todayUtc.AddDays(7));
        task.Status = "Pending";
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Generate alert for task assignment
        if (!string.IsNullOrWhiteSpace(task.AssignedToId))
        {
            var assignedUser = await _userManager.FindByIdAsync(task.AssignedToId);
            if (assignedUser != null)
            {
                var alertMessage = $"You have been assigned to task '{task.Title}' in {task.Group?.Name}";
                await _alertService.CreateAlertAsync(task.AssignedToId, task.Id, "TaskAssignment", alertMessage);
            }
        }

        _logger.LogInformation("Task request {TaskId} approved by {ApproverId} - AssignedTo: {AssignedToId} StartDate: {StartDate} DueDate: {DueDate}", task.Id, currentUser.Id, task.AssignedToId ?? "Unassigned", task.StartDate, task.DueDate);

        if (TempData["WarningMessage"] == null)
            TempData["SuccessMessage"] = "Task request approved and scheduled.";
        else
            TempData["SuccessMessage"] = "Task request approved.";

        return RedirectToAction("Details", "Groups", new { id = task.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectRequest(int taskId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive) return BadRequest("Cannot modify tasks for an archived or missing group.");

        var group = task.Group;
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdminFinal = User.IsInRole("Admin");
        bool isProfessorFinal = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLeadFinal = currentMember?.Role == "Lead";

        if (!isAdminFinal && !isProfessorFinal && !isLeadFinal) return Forbid();

        if (task.Status != "Requested")
        {
            TempData["ErrorMessage"] = "Task is not in requested state.";
            return RedirectToAction("Details", new { id = task.GroupId });
        }

        task.Status = "Rejected";
        task.UpdatedAt = DateTime.UtcNow;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Task request rejected.";
        return RedirectToAction("Details", new { id = task.GroupId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id, int notesPage = 1)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var task = await _context.Tasks
                .Include(t => t.Group).ThenInclude(g => g.Members)
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (task == null)
            {
                _logger.LogWarning("Task not found: Id={TaskId} requested by User={UserId}", id, currentUser?.Id);
                TempData["ErrorMessage"] = "Task not found.";
                return RedirectToAction("Index");
            }

            // Authorization: admins and professors can view; others only if assigned or group member
            bool isAdmin = User.IsInRole("Admin");
            bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

            if (!isAdmin && !isProfessor)
            {
                var isMember = task.Group?.Members?.Any(m => m.UserId == currentUser.Id) ?? false;
                var isAssigned = task.AssignedToId == currentUser.Id;
                if (!isMember && !isAssigned)
                    return Forbid();
            }

            var vm = new global::TeamSync.ViewModels.TaskListItemViewModel
            {
                Id = task.Id,
                GroupId = task.GroupId,
                GroupName = task.Group?.Name,
                Title = task.Title,
                Description = task.Description,
                Status = task.Status,
                AssignedToId = task.AssignedToId,
                AssignedToName = task.AssignedTo != null ? $"{task.AssignedTo.FirstName} {task.AssignedTo.LastName}" : null,
                CreatedById = task.CreatedById,
                CreatedByName = task.CreatedBy != null ? $"{task.CreatedBy.FirstName} {task.CreatedBy.LastName}" : null,
                StartDate = task.StartDate,
                DueDate = task.DueDate,
                Priority = task.Priority,
                ReviewRequestedById = task.ReviewRequestedById,
                ReviewRequestedAt = task.ReviewRequestedAt,
                LeadApprovedById = task.LeadApprovedById,
                LeadApprovedAt = task.LeadApprovedAt,
                CompletionApprovedById = task.CompletionApprovedById,
                CompletionApprovedAt = task.CompletionApprovedAt,
                ApprovalNotes = task.ApprovalNotes
            };

            // Resolve names for review/completion actors if present
            if (!string.IsNullOrEmpty(vm.ReviewRequestedById))
            {
                var user = await _context.Users.FindAsync(vm.ReviewRequestedById);
                if (user != null) vm.ReviewRequestedByName = $"{user.FirstName} {user.LastName}";
            }
            if (!string.IsNullOrEmpty(vm.LeadApprovedById))
            {
                var user = await _context.Users.FindAsync(vm.LeadApprovedById);
                if (user != null) vm.LeadApprovedByName = $"{user.FirstName} {user.LastName}";
            }
            if (!string.IsNullOrEmpty(vm.CompletionApprovedById))
            {
                var user = await _context.Users.FindAsync(vm.CompletionApprovedById);
                if (user != null) vm.CompletionApprovedByName = $"{user.FirstName} {user.LastName}";
            }

            // Flags for UI actions
            ViewBag.IsAssigned = task.AssignedToId == currentUser.Id;
            var currentMember = task.Group?.Members?.FirstOrDefault(m => m.UserId == currentUser.Id);
            bool isLead = currentMember?.Role == "Lead";
            // reuse previously-declared isAdmin/isProfessor variables (do not redeclare)
            isAdmin = User.IsInRole("Admin");
            isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

            // expose flags on viewmodel for Details view
            vm.IsLeadForCurrentUser = isLead;
            vm.IsProfessorForCurrentUser = isProfessor || isAdmin;

            // Can approve completion if admin, professor, or lead
            ViewBag.CanApproveCompletion = isAdmin || isProfessor || isLead;

            // Can archive if admin, professor, or lead (and not already archived)
            ViewBag.CanArchiveTask = (isAdmin || isProfessor || isLead) && !task.ArchivedAt.HasValue;
            ViewBag.IsArchived = task.ArchivedAt.HasValue;

            // Load assignments
            var assignments = await _context.TaskAssignments
                .Where(ta => ta.TaskId == id && ta.RemovedAt == null)
                .Include(ta => ta.AssignedTo)
                .OrderBy(ta => ta.AssignedAt)
                .ToListAsync();

            // Pagination for notes
            const int pageSize = 8;
            if (notesPage < 1) notesPage = 1;

            var notesQuery = _context.TaskNotes
                .Where(tn => tn.TaskId == id)
                .Include(tn => tn.User)
                .Include(tn => tn.Attachments)
                .OrderByDescending(tn => tn.CreatedAt);

            var totalNotes = await notesQuery.CountAsync();
            var totalPages = (int)Math.Ceiling(totalNotes / (double)pageSize);
            if (totalPages == 0) totalPages = 1;
            if (notesPage > totalPages) notesPage = totalPages;

            var notes = await notesQuery
                .Skip((notesPage - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Ensure User navigation is loaded for each note
            foreach (var note in notes)
            {
                if (note.User == null && !string.IsNullOrEmpty(note.UserId))
                {
                    note.User = await _context.Users.FindAsync(note.UserId);
                }
            }

            // Load contributions for display
            var contributions = await _context.Contributions
                .Where(c => c.TaskId == id)
                .Include(c => c.User)
                .Include(c => c.Overrides).ThenInclude(co => co.OverriddenBy)
                .Include(c => c.Overrides).ThenInclude(co => co.DisputedBy)
                .OrderByDescending(c => c.ContributedAt)
                .ToListAsync();

            ViewBag.TaskAssignments = assignments;
            ViewBag.TaskNotes = notes;
            ViewBag.TaskContributions = contributions;
            ViewBag.CurrentUserId = currentUser.Id;

            // Pagination metadata
            ViewBag.NotesPage = notesPage;
            ViewBag.NotesTotalPages = totalPages;
            ViewBag.NotesTotalCount = totalNotes;
            ViewBag.NotesPageSize = pageSize;

            return View(vm);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading task details for ID {TaskId}", id);
            TempData["ErrorMessage"] = $"Error loading task details: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    /// <summary>
    /// Helper method to handle file attachment uploads for task notes
    /// </summary>
    private async Task<List<FileAttachment>> ProcessFileUploads(int taskNoteId, IFormFileCollection files, User uploadedByUser)
    {
        var attachments = new List<FileAttachment>();

        if (files == null || files.Count == 0)
            return attachments;

        var fileUploadSettings = _configuration.GetSection("FileUploadSettings");
        var maxFileSizeBytes = fileUploadSettings.GetValue<long>("MaxFileSizeBytes", 10485760); // 10MB default
        var allowedExtensions = fileUploadSettings.GetSection("AllowedExtensions").Get<string[]>() ?? new string[] { };
        var storagePath = fileUploadSettings.GetValue<string>("StoragePath", "wwwroot/uploads/task-notes");

        foreach (var file in files)
        {
            if (file.Length == 0) continue;

            // Validate file size
            if (file.Length > maxFileSizeBytes)
            {
                TempData["WarningMessage"] = $"File {file.FileName} exceeds maximum size limit.";
                continue;
            }

            // Validate file extension
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                TempData["WarningMessage"] = $"File type {fileExtension} is not allowed.";
                continue;
            }

            try
            {
                // Generate unique filename to prevent conflicts
                var uniqueFileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}{fileExtension}";
                string filePath;

                if (_blobStorageService.IsConfigured())
                {
                    // Use Azure Blob Storage for production
                    using (var stream = file.OpenReadStream())
                    {
                        var blobUri = await _blobStorageService.UploadBlobAsync("task-attachments", uniqueFileName, stream);
                        filePath = blobUri;
                    }
                }
                else
                {
                    // Fall back to local file storage for development
                    var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), storagePath);
                    if (!Directory.Exists(uploadsDir))
                        Directory.CreateDirectory(uploadsDir);

                    var localFilePath = Path.Combine(uploadsDir, uniqueFileName);
                    using (var stream = new FileStream(localFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    filePath = $"/uploads/task-notes/{uniqueFileName}";
                }

                // Create FileAttachment record
                var attachment = new FileAttachment
                {
                    TaskNoteId = taskNoteId,
                    FileName = file.FileName,
                    FileType = file.ContentType ?? "application/octet-stream",
                    FileSize = file.Length,
                    FilePath = filePath,
                    UploadedByUserId = uploadedByUser.Id,
                    UploadedAt = DateTime.UtcNow
                };

                attachments.Add(attachment);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file {file.FileName}: {ex.Message}");
                TempData["WarningMessage"] = $"Error uploading file {file.FileName}.";
            }
        }

        return attachments;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int taskId, string content, IFormFileCollection files)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (string.IsNullOrWhiteSpace(content) && (files == null || files.Count == 0))
        {
            TempData["ErrorMessage"] = "Note cannot be empty and must contain either text or files.";
            return RedirectToAction("Details", new { id = taskId });
        }

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
        {
            TempData["ErrorMessage"] = "Cannot add note to archived or missing group.";
            return RedirectToAction("Details", new { id = taskId });
        }

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";
        bool isMember = currentMember != null;
        bool isAssigned = task.AssignedToId == currentUser.Id;

        if (!isAdmin && !isProfessor && !isLead && !isMember && !isAssigned)
            return Forbid();

        var note = new TaskNote
        {
            TaskId = taskId,
            UserId = currentUser.Id,
            Content = content?.Trim() ?? string.Empty,
            CreatedAt = DateTime.UtcNow
        };

        _context.TaskNotes.Add(note);
        await _context.SaveChangesAsync();

        // Process file attachments
        if (files != null && files.Count > 0)
        {
            var attachments = await ProcessFileUploads(note.Id, files, currentUser);
            if (attachments.Count > 0)
            {
                _context.FileAttachments.AddRange(attachments);
                await _context.SaveChangesAsync();
            }
        }

        TempData["SuccessMessage"] = "Note added.";
        return RedirectToAction("Details", new { id = taskId, notesPage = 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditNote(int noteId, string content, int? notesPage)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var note = await _context.TaskNotes.Include(n => n.Task).FirstOrDefaultAsync(n => n.Id == noteId);
        if (note == null) return NotFound();

        var task = note.Task;
        if (task == null) return BadRequest();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

        if (note.UserId != currentUser.Id && !isAdmin && !isProfessor)
            return Forbid();

        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["ErrorMessage"] = "Note cannot be empty.";
            return RedirectToAction("Details", new { id = task.Id, notesPage = notesPage });
        }

        note.Content = content.Trim();
        note.UpdatedAt = DateTime.UtcNow;

        _context.TaskNotes.Update(note);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Note updated.";
        return RedirectToAction("Details", new { id = task.Id, notesPage = notesPage });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteNote(int noteId, int? notesPage)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var note = await _context.TaskNotes.Include(n => n.Task).FirstOrDefaultAsync(n => n.Id == noteId);
        if (note == null) return NotFound();

        var task = note.Task;
        if (task == null) return BadRequest();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

        if (note.UserId != currentUser.Id && !isAdmin && !isProfessor)
            return Forbid();

             _context.TaskNotes.Remove(note);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Note deleted.";
            return RedirectToAction("Details", new { id = task.Id, notesPage = notesPage });
        }

        [HttpGet]
        public async Task<IActionResult> DownloadAttachment(int attachmentId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Challenge();

            var attachment = await _context.FileAttachments
                .Include(fa => fa.TaskNote)
                .ThenInclude(tn => tn.Task)
                .ThenInclude(t => t.Group)
                .ThenInclude(g => g.Members)
                .FirstOrDefaultAsync(fa => fa.Id == attachmentId);

            if (attachment == null) return NotFound();

            var taskNote = attachment.TaskNote;
            if (taskNote == null || taskNote.Task == null || taskNote.Task.Group == null)
                return NotFound();

            // Check if user has access to this task
            bool isAdmin = User.IsInRole("Admin");
            bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
            var isMember = taskNote.Task.Group.Members.Any(m => m.UserId == currentUser.Id && m.Group.IsActive);

            if (!isAdmin && !isProfessor && !isMember)
                return Forbid();

            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot" + attachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString()));

            if (!System.IO.File.Exists(filePath))
                return NotFound("File not found");

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, attachment.FileType, attachment.FileName);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetInProgress(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.AssignedToId != currentUser.Id) return Forbid();

        if (task.Status == "Completed")
        {
            TempData["ErrorMessage"] = "Task is already completed.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        task.Status = "InProgress";
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Notify task creator and group leads/professors that task is in progress
        var recipientIds = new HashSet<string>();
        if (!string.IsNullOrEmpty(task.CreatedById))
            recipientIds.Add(task.CreatedById);

        // Add assigned user to recipients
        if (!string.IsNullOrEmpty(task.AssignedToId))
            recipientIds.Add(task.AssignedToId);

        if (task.Group?.Members != null)
        {
            foreach (var member in task.Group.Members)
            {
                if (member.User != null && (member.Role == "Lead" || await _userManager.IsInRoleAsync(member.User, "Professor")))
                    recipientIds.Add(member.UserId);
            }
        }

        if (recipientIds.Any())
        {
            await _notificationService.CreateNotificationsForUsersAsync(
                recipientIds.ToList(),
                "StatusChange",
                $"Task '{task.Title}' is now in progress.",
                task.Id);

            // Generate status change alerts
            await _alertService.CreateAlertsAsync(
                recipientIds.ToList(),
                task.Id,
                "StatusChange",
                $"Task '{task.Title}' status changed to In Progress");
        }

        TempData["SuccessMessage"] = "Task started.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReview(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.AssignedToId != currentUser.Id) return Forbid();

        if (task.Status != "InProgress")
        {
            TempData["ErrorMessage"] = "Task must be in progress to request review.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        task.Status = "ReviewRequested";
        task.ReviewRequestedById = currentUser.Id;
        task.ReviewRequestedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Notify creator, leads, and professors that review is requested
        var recipientIds = new HashSet<string>();
        if (!string.IsNullOrEmpty(task.CreatedById))
            recipientIds.Add(task.CreatedById);

        if (task.Group?.Members != null)
        {
            foreach (var member in task.Group.Members)
            {
                if (member.User != null && (member.Role == "Lead" || await _userManager.IsInRoleAsync(member.User, "Professor")))
                    recipientIds.Add(member.UserId);
            }
        }

        if (recipientIds.Any())
        {
            await _notificationService.CreateNotificationsForUsersAsync(
                recipientIds.ToList(),
                "ReviewRequest",
                $"Task '{task.Title}' is ready for review.",
                task.Id);

            // Generate alert for review request
            await _alertService.CreateAlertsAsync(
                recipientIds.ToList(),
                task.Id,
                "ApprovalRequested",
                $"Task '{task.Title}' is ready for review");
        }

        TempData["SuccessMessage"] = "Review requested.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    // New: allow an assignee to propose completion from Pending or InProgress
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCompleted(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.AssignedToId != currentUser.Id) return Forbid();

        if (task.Status == "Completed")
        {
            TempData["ErrorMessage"] = "Task is already completed.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        if (task.Status == "ReviewRequested" || task.Status == "LeadApproved")
        {
            TempData["ErrorMessage"] = "A review is already pending for this task.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // mark as review requested so creator/professor can approve
        task.Status = "ReviewRequested";
        task.ReviewRequestedById = currentUser.Id;
        task.ReviewRequestedAt = DateTime.UtcNow;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        // Notify creator, leads, and professors that completion is proposed
        var recipientIds = new HashSet<string>();
        if (!string.IsNullOrEmpty(task.CreatedById))
            recipientIds.Add(task.CreatedById);

        if (task.Group?.Members != null)
        {
            foreach (var member in task.Group.Members)
            {
                if (member.User != null && (member.Role == "Lead" || await _userManager.IsInRoleAsync(member.User, "Professor")))
                    recipientIds.Add(member.UserId);
            }
        }

        if (recipientIds.Any())
        {
            await _notificationService.CreateNotificationsForUsersAsync(
                recipientIds.ToList(),
                "CompletionProposal",
                $"Task '{task.Title}' completion is pending approval.",
                task.Id);
        }

        TempData["SuccessMessage"] = "Completion proposed; awaiting approval.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCompletion(int id, string? notes, decimal? hours)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group?.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";

        if (task.Status != "ReviewRequested" && task.Status != "LeadApproved")
        {
            TempData["ErrorMessage"] = "No review pending for this task.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // basic server-side validation for notes
        if (!string.IsNullOrWhiteSpace(notes) && notes.Length > 2000)
        {
            TempData["ErrorMessage"] = "Approval notes cannot exceed 2000 characters.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // validate hours if provided
        if (hours.HasValue && (hours < 0 || hours > 1000))
        {
            TempData["ErrorMessage"] = "Hours must be between 0 and 1000.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Allow creator to finalize, or professors/admins to override
        if (task.CreatedById == currentUser.Id || isProfessor || isAdmin)
        {
            task.Status = "Completed";
            task.CompletionApprovedById = currentUser.Id;
            task.CompletionApprovedAt = DateTime.UtcNow;
            task.ApprovalNotes = string.IsNullOrWhiteSpace(notes) ? task.ApprovalNotes : notes?.Trim();
            task.UpdatedAt = DateTime.UtcNow;

            // create contribution record for the assignee if not already recorded
            if (!string.IsNullOrEmpty(task.AssignedToId))
            {
                var existing = await _context.Contributions.FirstOrDefaultAsync(c => c.TaskId == task.Id && c.UserId == task.AssignedToId);
                if (existing == null)
                {
                    var contribution = new Contribution
                    {
                        UserId = task.AssignedToId,
                        TaskId = task.Id,
                        Description = $"Completed task: {task.Title}",
                        ContributedAt = DateTime.UtcNow,
                        HoursSpent = hours,
                        RecordedById = currentUser.Id,
                        RecordedAt = DateTime.UtcNow,
                        Source = "TaskFinalization",
                        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes?.Trim()
                    };

                    _context.Contributions.Add(contribution);
                }
                else
                {
                    // update existing contribution if hours/notes provided
                    if (hours.HasValue) existing.HoursSpent = hours;
                    if (!string.IsNullOrWhiteSpace(notes)) existing.Notes = notes?.Trim();
                    existing.RecordedById = currentUser.Id;
                    existing.RecordedAt = DateTime.UtcNow;
                    _context.Contributions.Update(existing);
                }
            }

            // use transaction to ensure task and contribution are saved atomically
            using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    _context.Tasks.Update(task);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();

                    // Send notifications and generate alerts for assigned user and watchers
                    var recipientIds = new HashSet<string>();
                    if (!string.IsNullOrEmpty(task.AssignedToId))
                        recipientIds.Add(task.AssignedToId);
                    if (!string.IsNullOrEmpty(task.CreatedById))
                        recipientIds.Add(task.CreatedById);

                    // Add group lead and professors
                    if (task.Group?.Members != null)
                    {
                        foreach (var member in task.Group.Members)
                        {
                            if (member.User != null && (member.Role == "Lead" || 
                                await _userManager.IsInRoleAsync(member.User, "Professor")))
                            {
                                recipientIds.Add(member.UserId);
                            }
                        }
                    }

                    if (recipientIds.Any())
                    {
                        await _notificationService.CreateNotificationsForUsersAsync(
                            recipientIds.ToList(),
                            "StatusChange",
                            $"Task '{task.Title}' has been approved and marked as completed.",
                            task.Id);

                        // Generate alerts for approval notification
                        await _alertService.CreateAlertsAsync(
                            recipientIds.ToList(),
                            task.Id,
                            "ApprovalRequested",
                            $"Task '{task.Title}' has been approved and is now completed");
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["ErrorMessage"] = "The task was updated by another user. Your changes were not saved.";
                    return RedirectToAction("Details", new { id = task.Id });
                }
                catch (DbUpdateException dbex)
                {
                    // if unique constraint violated on contributions, treat as non-fatal and continue
                    _logger.LogWarning(dbex, "DB update error when adding contribution for task {TaskId}", task.Id);
                    TempData["WarningMessage"] = "Contribution already recorded by another process.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error approving task completion");
                    TempData["ErrorMessage"] = "An error occurred while processing your request. Please try again.";
                    return RedirectToAction("Details", new { id = task.Id });
                }
            }

            TempData["SuccessMessage"] = "Task marked as completed.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Leads can mark lead-approval (first step)
        if (isLead)
        {
            task.Status = "LeadApproved";
            task.LeadApprovedById = currentUser.Id;
            task.LeadApprovedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Task approved by lead. Awaiting professor finalization.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Others forbidden
        return Forbid();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCompletion(int id, string? reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group?.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";

        if (task.Status != "ReviewRequested" && task.Status != "LeadApproved")
        {
            TempData["ErrorMessage"] = "No review pending for this task.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // basic server-side validation for reason length
        if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 1000)
        {
            TempData["ErrorMessage"] = "Rejection reason cannot exceed 1000 characters.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // perform status transition in a transaction to avoid partial updates
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                // If professor/admin rejects, clear lead approval and set back to InProgress
                if (isProfessor || isAdmin)
                {
                    task.Status = "InProgress";
                    task.ReviewRequestedById = null;
                    task.ReviewRequestedAt = null;
                    task.LeadApprovedById = null;
                    task.LeadApprovedAt = null;
                    task.ApprovalNotes = string.IsNullOrWhiteSpace(reason) ? task.ApprovalNotes : reason?.Trim();
                    task.UpdatedAt = DateTime.UtcNow;

                    _context.Tasks.Update(task);
                    await _context.SaveChangesAsync();

                    // Generate alert for rejection notification
                    if (!string.IsNullOrEmpty(task.AssignedToId))
                    {
                        var alertMessage = $"Task '{task.Title}' has been rejected and sent back to In Progress";
                        if (!string.IsNullOrWhiteSpace(reason))
                        {
                            alertMessage += $": {reason}";
                        }
                        await _alertService.CreateAlertAsync(task.AssignedToId, task.Id, "ApprovalRejected", alertMessage);
                    }

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Review rejected; task set back to In Progress.";
                    return RedirectToAction("Details", new { id = task.Id });
                }

                // If lead rejects, revert to InProgress
                if (isLead)
                {
                    task.Status = "InProgress";
                    task.ReviewRequestedById = null;
                    task.ReviewRequestedAt = null;
                    task.LeadApprovedById = null;
                    task.LeadApprovedAt = null;
                    task.ApprovalNotes = string.IsNullOrWhiteSpace(reason) ? task.ApprovalNotes : reason?.Trim();
                    task.UpdatedAt = DateTime.UtcNow;

                    _context.Tasks.Update(task);
                    await _context.SaveChangesAsync();

                    // Generate alert for lead rejection notification
                    if (!string.IsNullOrEmpty(task.AssignedToId))
                    {
                        var alertMessage = $"Task '{task.Title}' has been rejected by lead and sent back to In Progress";
                        if (!string.IsNullOrWhiteSpace(reason))
                        {
                            alertMessage += $": {reason}";
                        }
                        await _alertService.CreateAlertAsync(task.AssignedToId, task.Id, "ApprovalRejected", alertMessage);
                    }

                    await transaction.CommitAsync();

                    TempData["SuccessMessage"] = "Lead rejected review; task set back to In Progress.";
                    return RedirectToAction("Details", new { id = task.Id });
                }

                return Forbid();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting task completion");
                TempData["ErrorMessage"] = "An error occurred while processing your request. Please try again.";
                return RedirectToAction("Details", new { id = task.Id });
            }
        }
    }

    /// <summary>
    /// Add a user as an assignee to a task (multi-assign support).
    /// Only leads can perform this action.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAssignee(int taskId, string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot modify tasks for an archived or missing group.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLead = currentMember?.Role == "Lead";

        // Only leads (and admins/professors) can add assignees
        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        // Verify the user to assign is a group member
        var targetMember = task.Group.Members.FirstOrDefault(m => m.UserId == userId);
        if (targetMember == null)
            return BadRequest("User must be a member of the group.");

        // Prevent assigning tasks to professors
        if (targetMember.User != null && await _userManager.IsInRoleAsync(targetMember.User, "Professor"))
        {
            TempData["ErrorMessage"] = "Tasks cannot be assigned to professors.";
            return RedirectToAction("Details", new { id = taskId });
        }

        // Check if already assigned
        var existingAssignment = await _context.TaskAssignments.FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.AssignedToId == userId && ta.RemovedAt == null);

        if (existingAssignment != null)
        {
            TempData["WarningMessage"] = "User is already assigned to this task.";
            return RedirectToAction("Details", new { id = taskId });
        }

        var assignment = new TaskAssignment { TaskId = taskId, AssignedToId = userId, AssignedByUserId = currentUser.Id, AssignedAt = DateTime.UtcNow };
        _context.TaskAssignments.Add(assignment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "User added as assignee.";
        return RedirectToAction("Details", new { id = taskId });
    }

    /// <summary>
    /// Remove a user from task assignees.
    /// Only leads can perform this action.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAssignee(int taskId, string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot modify tasks for an archived or missing group.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLead = currentMember?.Role == "Lead";

        // Only leads (and admins/professors) can remove assignees
        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        var assignment = await _context.TaskAssignments.FirstOrDefaultAsync(ta => ta.TaskId == taskId && ta.AssignedToId == userId && ta.RemovedAt == null);

        if (assignment == null)
        {
            TempData["ErrorMessage"] = "Assignment not found.";
            return RedirectToAction("Details", new { id = taskId });
        }

        assignment.RemovedAt = DateTime.UtcNow;
        _context.TaskAssignments.Update(assignment);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "User removed from assignees.";
        return RedirectToAction("Details", new { id = taskId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContribution(int taskId, string description, decimal? hours, string? notes, string? userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive) return BadRequest("Cannot add contribution to archived or missing group.");

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";
        bool isAssigned = task.AssignedToId == currentUser.Id;

        if (!isAdmin && !isProfessor && !isLead && !isAssigned)
            return Forbid();

        var attributedUserId = string.IsNullOrWhiteSpace(userId) ? (task.AssignedToId ?? currentUser.Id) : userId.Trim();

        // Validate that the attributed user exists in the database
        if (!string.IsNullOrWhiteSpace(attributedUserId))
        {
            var attributedUser = await _context.Users.FindAsync(attributedUserId);
            if (attributedUser == null)
            {
                TempData["ErrorMessage"] = "The specified user does not exist.";
                return RedirectToAction("Details", new { id = taskId });
            }
        }

        // Check if a contribution already exists for this task and user
        var existingContribution = await _context.Contributions
            .FirstOrDefaultAsync(c => c.TaskId == taskId && c.UserId == attributedUserId);

        // Determine if this is a student-submitted contribution
        // It's student-submitted if:
        // 1. The assignee is adding their own contribution
        // 2. AND they are not an admin, professor, or lead
        bool isStudentSubmitted = isAssigned && !isAdmin && !isProfessor && !isLead && attributedUserId == currentUser.Id;

        string action;
        Contribution contribution;

        if (existingContribution != null)
        {
            // Update existing contribution
            var oldDescription = existingContribution.Description;
            var oldHours = existingContribution.HoursSpent;
            var oldNotes = existingContribution.Notes;

            existingContribution.Description = (description ?? string.Empty).Trim();
            existingContribution.HoursSpent = hours;
            existingContribution.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
            existingContribution.RecordedById = currentUser.Id;
            existingContribution.RecordedAt = DateTime.UtcNow;

            _context.Contributions.Update(existingContribution);
            await _context.SaveChangesAsync();

            action = "Updated";
            contribution = existingContribution;

            // Create audit record for update
            var changes = JsonSerializer.Serialize(new
            {
                oldDescription,
                newDescription = existingContribution.Description,
                oldHours,
                newHours = existingContribution.HoursSpent,
                oldNotes,
                newNotes = existingContribution.Notes
            });
            var history = new ContributionHistory
            {
                ContributionId = contribution.Id,
                Action = action,
                PerformedById = currentUser.Id,
                PerformedAt = DateTime.UtcNow,
                Changes = changes
            };
            _context.ContributionHistories.Add(history);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Contribution updated.";
        }
        else
        {
            // Create new contribution
            contribution = new Contribution
            {
                TaskId = taskId,
                UserId = attributedUserId,
                Description = (description ?? string.Empty).Trim(),
                HoursSpent = hours,
                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
                RecordedById = currentUser.Id,
                RecordedAt = DateTime.UtcNow,
                ContributedAt = DateTime.UtcNow,
                IsStudentSubmitted = isStudentSubmitted
            };

            _context.Contributions.Add(contribution);
            await _context.SaveChangesAsync();

            action = "Created";

            // Create audit record for creation
            var changes = JsonSerializer.Serialize(new { contribution.UserId, contribution.Description, contribution.HoursSpent, contribution.Notes });
            var history = new ContributionHistory
            {
                ContributionId = contribution.Id,
                Action = action,
                PerformedById = currentUser.Id,
                PerformedAt = DateTime.UtcNow,
                Changes = changes
            };
            _context.ContributionHistories.Add(history);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Contribution added.";
        }

        return RedirectToAction("Details", new { id = taskId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditContribution(int contributionId, string? description, decimal? hours, string? notes, string? justification)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var contribution = await _context.Contributions
            .Include(c => c.Task)
            .Include(c => c.Overrides)
            .FirstOrDefaultAsync(c => c.Id == contributionId);
        if (contribution == null) return NotFound();

        var task = contribution.Task;
        if (task == null) return BadRequest();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group?.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";

        // Authorization: Only admin, professor, or lead can edit
        // If contribution is student-submitted, only admin/professor can override
        if (contribution.IsStudentSubmitted && !isAdmin && !isProfessor)
            return Forbid("Only professors and admins can override student submissions");

        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        // If this is a student-submitted contribution, create an override instead of mutating
        if (contribution.IsStudentSubmitted && (isAdmin || isProfessor || isLead))
        {
            // Create override record instead of modifying original
            var overrideRecord = new Models.ContributionOverride
            {
                ContributionId = contribution.Id,
                OverriddenById = currentUser.Id,
                OverriddenAt = DateTime.UtcNow,
                OriginalHours = contribution.HoursSpent,
                NewHours = hours,
                OriginalDescription = contribution.Description,
                NewDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Justification = string.IsNullOrWhiteSpace(justification) 
                    ? "No justification provided" 
                    : justification.Trim(),
                IsApproved = true
            };

            _context.ContributionOverrides.Add(overrideRecord);

            // Create audit trail
            var changes = JsonSerializer.Serialize(new
            {
                action = "OverrideCreated",
                originalHours = contribution.HoursSpent,
                newHours = hours,
                originalDescription = contribution.Description,
                newDescription = description,
                justification = overrideRecord.Justification
            });

            var history = new ContributionHistory
            {
                ContributionId = contribution.Id,
                Action = "Overridden",
                PerformedById = currentUser.Id,
                PerformedAt = DateTime.UtcNow,
                Changes = changes
            };
            _context.ContributionHistories.Add(history);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Contribution override recorded. Original submission preserved.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // For lead-created (non-student-submitted) contributions, allow direct editing
        var before = JsonSerializer.Serialize(new { contribution.UserId, contribution.Description, contribution.HoursSpent, contribution.Notes });

        contribution.Description = (description ?? string.Empty).Trim();
        contribution.HoursSpent = hours;
        contribution.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

        _context.Contributions.Update(contribution);
        await _context.SaveChangesAsync();

        var after = JsonSerializer.Serialize(new { contribution.UserId, contribution.Description, contribution.HoursSpent, contribution.Notes });

        var historyRecord = new ContributionHistory
        {
            ContributionId = contribution.Id,
            Action = "Updated",
            PerformedById = currentUser.Id,
            PerformedAt = DateTime.UtcNow,
            Changes = JsonSerializer.Serialize(new { before, after })
        };
        _context.ContributionHistories.Add(historyRecord);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Contribution updated.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteContribution(int contributionId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var contribution = await _context.Contributions.Include(c => c.Task).FirstOrDefaultAsync(c => c.Id == contributionId);
        if (contribution == null) return NotFound();

        var task = contribution.Task;
        if (task == null) return BadRequest();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

        if (contribution.RecordedById != currentUser.Id && !isAdmin && !isProfessor)
            return Forbid();

        var snapshot = JsonSerializer.Serialize(new { contribution.UserId, contribution.Description, contribution.HoursSpent, contribution.Notes });

        var history = new ContributionHistory
        {
            ContributionId = contribution.Id,
            Action = "Deleted",
            PerformedById = currentUser.Id,
            PerformedAt = DateTime.UtcNow,
            Changes = snapshot
        };

        _context.ContributionHistories.Add(history);
        _context.Contributions.Remove(contribution);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Contribution deleted.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpGet]
    public async Task<IActionResult> ContributionHistory(int contributionId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var history = await _context.ContributionHistories
            .Where(h => h.ContributionId == contributionId)
            .OrderByDescending(h => h.PerformedAt)
            .ToListAsync();

        if (!history.Any()) return NotFound();

        return View("ContributionHistory", history);
    }

    [HttpGet]
    public async Task<IActionResult> ExportContributionsCsv(int taskId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).FirstOrDefaultAsync(t => t.Id == taskId);
        if (task == null) return NotFound();
        if (task.Group == null) return BadRequest();

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";
        bool isMember = currentMember != null;

        if (!isAdmin && !isProfessor && !isLead && !isMember && task.AssignedToId != currentUser.Id)
            return Forbid();

        var contributions = await _context.Contributions
            .Where(c => c.TaskId == taskId)
            .Include(c => c.User)
            .Include(c => c.RecordedBy)
            .Include(c => c.Task).ThenInclude(t => t.Group)
            .OrderByDescending(c => c.ContributedAt)
            .ToListAsync();

        var bytes = global::TeamSync.Services.CsvExportService.GenerateContributionsCsvBytes(contributions, task, task.Group);

        return File(bytes, "text/csv; charset=utf-8", $"contributions_task_{taskId}.csv");
    }

    /// <summary>
    /// Archive a task - soft delete with audit trail.
    /// Only Lead/Professor/Admin can archive.
    /// Cannot archive completed/rejected/already archived tasks.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveTask(int id, string? reason)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot archive tasks for archived or missing group.");

        // Authorization: Lead/Professor/Admin only
        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isLead = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        // Cannot archive already archived tasks
        if (task.ArchivedAt.HasValue)
        {
            TempData["ErrorMessage"] = "Task is already archived.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Cannot archive completed tasks (preserve history)
        if (task.Status == "Completed")
        {
            TempData["ErrorMessage"] = "Cannot archive completed tasks. They are preserved for accountability.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Cannot archive rejected tasks (preserve history)
        if (task.Status == "Rejected")
        {
            TempData["ErrorMessage"] = "Cannot archive rejected tasks. They are preserved for accountability.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Validate reason length
        if (!string.IsNullOrWhiteSpace(reason) && reason.Length > 1000)
        {
            TempData["ErrorMessage"] = "Archive reason cannot exceed 1000 characters.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Archive the task (soft delete)
        task.ArchivedAt = DateTime.UtcNow;
        task.ArchivedById = currentUser.Id;
        task.ArchiveReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} archived by {UserId} with reason: {Reason}", 
            task.Id, currentUser.Id, task.ArchiveReason ?? "No reason provided");

        TempData["SuccessMessage"] = "Task archived successfully.";
        return RedirectToAction("Details", "Groups", new { id = task.GroupId });
    }

    /// <summary>
    /// Restore an archived task.
    /// Only the user who archived it, or Admin/Professor can restore.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreTask(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).ThenInclude(g => g.Members).FirstOrDefaultAsync(t => t.Id == id);
        if (task == null) return NotFound();
        if (task.Group == null || !task.Group.IsActive)
            return BadRequest("Cannot restore tasks for archived or missing group.");

        // Authorization: Task archiver, Professor, Admin
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        bool isArchiver = task.ArchivedById == currentUser.Id;

        if (!isAdmin && !isProfessor && !isArchiver)
            return Forbid();

        // Cannot restore if not archived
        if (!task.ArchivedAt.HasValue)
        {
            TempData["ErrorMessage"] = "Task is not archived.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // Restore the task
        task.ArchivedAt = null;
        task.ArchivedById = null;
        task.ArchiveReason = null;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task {TaskId} restored by {UserId}", task.Id, currentUser.Id);

        TempData["SuccessMessage"] = "Task restored successfully.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    /// <summary>
    /// Hard delete a task (admin only).
    /// Only Admin can permanently delete tasks.
    /// Removes all associated data (contributions, notes, assignments).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HardDeleteTask(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        // Admin only
        if (!User.IsInRole("Admin"))
            return Forbid();

        var task = await _context.Tasks
            .Include(t => t.Contributions)
            .Include(t => t.Notes)
            .Include(t => t.Assignments)
            .Include(t => t.Group)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();

        int groupId = task.GroupId ?? 0;

        // Delete all related records first
        _context.ContributionHistories.RemoveRange(
            await _context.ContributionHistories
                .Where(ch => ch.Contribution.TaskId == task.Id)
                .ToListAsync()
        );

        _context.Contributions.RemoveRange(task.Contributions);
        _context.TaskNotes.RemoveRange(task.Notes);
        _context.TaskAssignments.RemoveRange(task.Assignments);

        // Delete the task itself
        _context.Tasks.Remove(task);
        await _context.SaveChangesAsync();

        _logger.LogWarning("Task {TaskId} permanently deleted by Admin {UserId}", task.Id, currentUser.Id);

        TempData["SuccessMessage"] = "Task permanently deleted.";
        return RedirectToAction("Details", "Groups", new { id = groupId });
    }

    /// <summary>
    /// View archived tasks for a group.
    /// Only Lead/Professor/Admin can view archived tasks.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> ArchivedTasks(int groupId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) return NotFound();

        // Authorization: Member of group, Lead, Professor, or Admin
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";
        bool isMember = currentMember != null;

        if (!isAdmin && !isProfessor && !isLead && !isMember)
            return Forbid();

        var archivedTasks = await _context.Tasks
            .Where(t => t.GroupId == groupId && t.ArchivedAt.HasValue)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.ArchivedBy)
            .OrderByDescending(t => t.ArchivedAt)
            .ToListAsync();

        ViewBag.GroupId = groupId;
        ViewBag.GroupName = group.Name;
        ViewBag.CanManageTasks = isAdmin || isProfessor || isLead;

        return View(archivedTasks);
    }

    /// <summary>
    /// Serves attachment images with authentication and access control.
    /// This endpoint allows authorized users to view images stored in Azure Blob Storage or local storage.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAttachmentImage(int attachmentId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var attachment = await _context.FileAttachments
            .Include(fa => fa.TaskNote)
            .ThenInclude(tn => tn.Task)
            .ThenInclude(t => t.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(fa => fa.Id == attachmentId);

        if (attachment == null) return NotFound();

        // Check if this is an image
        if (!attachment.IsImage) return BadRequest("This attachment is not an image.");

        var taskNote = attachment.TaskNote;
        if (taskNote == null || taskNote.Task == null || taskNote.Task.Group == null)
            return NotFound();

        // Check if user has access to this task
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
        var isMember = taskNote.Task.Group.Members.Any(m => m.UserId == currentUser.Id && m.Group.IsActive);

        if (!isAdmin && !isProfessor && !isMember)
            return Forbid();

        try
        {
            byte[] imageBytes;

            // Check if FilePath is a blob URL (Azure Blob Storage)
            if (attachment.FilePath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                // Extract blob name from the FilePath URL
                // URL format: https://{account}.blob.core.windows.net/{container}/{blobname}
                var uri = new Uri(attachment.FilePath);
                var blobName = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

                if (string.IsNullOrEmpty(blobName))
                {
                    _logger.LogError($"Could not extract blob name from FilePath: {attachment.FilePath}");
                    return NotFound("Invalid blob path.");
                }

                // Download from Azure Blob Storage using authenticated service
                try
                {
                    imageBytes = await _blobStorageService.DownloadBlobAsync("task-attachments", blobName);
                }
                catch (Azure.RequestFailedException ex)
                {
                    _logger.LogError($"Azure error retrieving blob {blobName}: {ex.Message}");
                    return NotFound("Image not found in blob storage.");
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error downloading blob {blobName}: {ex.Message}");
                    return StatusCode(500, "Error retrieving image from blob storage.");
                }
            }
            else
            {
                // Serve from local file system
                var filePath = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot" + attachment.FilePath.Replace("/", Path.DirectorySeparatorChar.ToString())
                );

                if (!System.IO.File.Exists(filePath))
                    return NotFound("Image file not found.");

                imageBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            }

            // Return the image with appropriate content type
            return File(imageBytes, attachment.FileType ?? "image/jpeg", attachment.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving attachment image {attachmentId}: {ex.Message}");
            return StatusCode(500, "Error retrieving image.");
        }
    }
}

