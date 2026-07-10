using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<TasksController> _logger;

    public TasksController(ApplicationDbContext context, UserManager<User> userManager, ILogger<TasksController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? status)
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

                // compute CanApprove for current user
                CanApprove = isAdmin || isProfessor || isLeadForThis,
                IsLeadForCurrentUser = isLeadForThis,
                IsProfessorForCurrentUser = isProfessorForCurrentUser
            };
        }).ToList();

        var canCreateTask = isAdmin || isProfessor || isLeadInAnyGroup;
        ViewBag.ActiveStatus = activeStatus;
        ViewBag.CanCreateTask = canCreateTask;
        ViewBag.CanRequestTask = !canCreateTask && isMemberInAnyGroup;
        ViewBag.CurrentUserId = currentUser.Id; // used by view to show card-level actions

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
        var vm = new TaskCreateViewModel { GroupId = groupId, DueDate = DateTime.UtcNow.AddDays(7) };
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
            ViewBag.Members = group.Members.Select(m => m.User).Where(u => u != null).ToList();
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
        }

        var task = new global::TeamSync.Models.Task
        {
            Title = model.Title,
            Description = model.Description,
            GroupId = model.GroupId,
            AssignedToId = model.AssignedToId,
            CreatedById = currentUser.Id,
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
        if (!isStudent && !User.IsInRole("Student")) return Forbid();

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
        if (!isStudent && !User.IsInRole("Student")) return Forbid();

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
    public async Task<IActionResult> ApproveRequest(int taskId, DateTime? dueDate, string? assignedToId)
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

        if (!isAdminLocal && !isProfessorLocal && !isLeadLocal)
            return Forbid();

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

        task.DueDate = (dueDate?.Date ?? todayUtc.AddDays(7));
        task.Status = "Pending";
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Task request {TaskId} approved by {ApproverId} - AssignedTo: {AssignedToId} DueDate: {DueDate}", task.Id, currentUser.Id, task.AssignedToId ?? "Unassigned", task.DueDate);

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

        if (!isAdminFinal && !isProfessorFinal && !isLeadFinal)
            return Forbid();

        if (task.Status != "Requested")
        {
            TempData["ErrorMessage"] = "Task is not in requested state.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        task.Status = "Rejected";
        task.UpdatedAt = DateTime.UtcNow;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Task request rejected.";
        return RedirectToAction("Details", "Groups", new { id = task.GroupId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
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
            var isMember = task.Group?.Members.Any(m => m.UserId == currentUser.Id) ?? false;
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
            DueDate = task.DueDate,
            Priority = task.Priority,
            ReviewRequestedById = task.ReviewRequestedById,
            ReviewRequestedAt = task.ReviewRequestedAt,
            LeadApprovedById = task.LeadApprovedById,
            LeadApprovedAt = task.LeadApprovedAt,
            CompletionApprovedById = task.CompletionApprovedById,
            CompletionApprovedAt = task.CompletionApprovedAt
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
        var currentMember = task.Group?.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isLead = currentMember?.Role == "Lead";
        // reuse previously-declared isAdmin/isProfessor variables (do not redeclare)
        isAdmin = User.IsInRole("Admin");
        isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");

        // expose flags on viewmodel for Details view
        vm.IsLeadForCurrentUser = isLead;
        vm.IsProfessorForCurrentUser = isProfessor || isAdmin;

        // Can approve completion if admin, professor, or lead
        ViewBag.CanApproveCompletion = isAdmin || isProfessor || isLead;

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (task == null) return NotFound();
        if (task.Group == null) return BadRequest("Task's group is missing.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        // Only allow creator, admins, professors, or leads to edit
        if (!isAdmin && !isProfessor && !isLead && task.CreatedById != currentUser.Id)
            return Forbid();

        var vm = new global::TeamSync.ViewModels.TaskEditViewModel
        {
            Id = task.Id,
            GroupId = task.GroupId ?? 0,
            Title = task.Title,
            Description = task.Description,
            AssignedToId = task.AssignedToId,
            DueDate = task.DueDate,
            Priority = task.Priority
        };

        ViewBag.Members = task.Group.Members.Select(m => m.User).Where(u => u != null).ToList();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(global::TeamSync.ViewModels.TaskEditViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        if (!ModelState.IsValid) return View(model);

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members).ThenInclude(m => m.User)
            .FirstOrDefaultAsync(t => t.Id == model.Id);

        if (task == null) return NotFound();
        if (task.Group == null) return BadRequest("Task's group is missing.");

        var currentMember = task.Group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        // Only allow creator, admins, professors, or leads to edit
        if (!isAdmin && !isProfessor && !isLead && task.CreatedById != currentUser.Id)
            return Forbid();

        // If assigning, ensure AssignedToId is a member
        if (!string.IsNullOrEmpty(model.AssignedToId))
        {
            var assignedIsMember = task.Group.Members.Any(m => m.UserId == model.AssignedToId);
            if (!assignedIsMember)
            {
                ModelState.AddModelError("AssignedToId", "Assigned user must be a member of the group.");
                ViewBag.Members = task.Group.Members.Select(m => m.User).Where(u => u != null).ToList();
                return View(model);
            }
        }

        // Apply changes
        task.Title = model.Title;
        task.Description = model.Description;
        task.AssignedToId = model.AssignedToId;
        task.DueDate = model.DueDate;
        task.Priority = model.Priority;
        task.UpdatedAt = DateTime.UtcNow;

        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Task updated successfully.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpGet]
    public async Task<IActionResult> SelectGroup(string mode = "create")
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var normalizedMode = (mode ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizedMode != "create" && normalizedMode != "request")
            return BadRequest("Invalid mode.");

        var activeGroupsQuery = _context.Groups
            .Include(g => g.Members)
            .Where(g => g.IsActive)
            .AsQueryable();

        List<TaskGroupSelectionItemViewModel> groups;

        if (normalizedMode == "create")
        {
            bool isAdmin = User.IsInRole("Admin");
            bool isProfessorGlobal = await _userManager.IsInRoleAsync(currentUser, "Professor");

            if (!isAdmin && !isProfessorGlobal)
            {
                activeGroupsQuery = activeGroupsQuery
                    .Where(g => g.Members.Any(m => m.UserId == currentUser.Id && (m.Role == "Lead" || m.Role == "Professor")));
            }

            groups = await activeGroupsQuery
                .Select(g => new TaskGroupSelectionItemViewModel
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    GroupDescription = g.Description
                })
                .ToListAsync();
        }
        else
        {
            groups = await activeGroupsQuery
                .Where(g => g.Members.Any(m => m.UserId == currentUser.Id && m.Role == "Member"))
                .Select(g => new TaskGroupSelectionItemViewModel
                {
                    GroupId = g.Id,
                    GroupName = g.Name,
                    GroupDescription = g.Description
                })
                .ToListAsync();
        }

        if (!groups.Any())
        {
            TempData["ErrorMessage"] = normalizedMode == "create"
                ? "No active groups available where you can create tasks."
                : "No active groups available where you can request tasks.";
            return RedirectToAction("Index");
        }

        if (groups.Count == 1)
        {
            var groupId = groups[0].GroupId;
            return normalizedMode == "create"
                ? RedirectToAction("Create", new { groupId })
                : RedirectToAction("RequestTask", new { groupId });
        }

        var vm = new TaskGroupSelectionViewModel
        {
            Mode = normalizedMode,
            Groups = groups.OrderBy(g => g.GroupName).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetInProgress(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).FirstOrDefaultAsync(t => t.Id == id);
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

        TempData["SuccessMessage"] = "Task started.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestReview(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks.Include(t => t.Group).FirstOrDefaultAsync(t => t.Id == id);
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

        TempData["SuccessMessage"] = "Review requested.";
        return RedirectToAction("Details", new { id = task.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCompletion(int id)
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

        // Professors/admins finalize completion
        if (isProfessor || isAdmin)
        {
            task.Status = "Completed";
            task.CompletionApprovedById = currentUser.Id;
            task.CompletionApprovedAt = DateTime.UtcNow;
            task.UpdatedAt = DateTime.UtcNow;

            // clear lead approval on finalization? keep record
            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();

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
    public async Task<IActionResult> RejectCompletion(int id)
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

        // If professor/admin rejects, clear lead approval and set back to InProgress
        if (isProfessor || isAdmin)
        {
            task.Status = "InProgress";
            task.ReviewRequestedById = null;
            task.ReviewRequestedAt = null;
            task.LeadApprovedById = null;
            task.LeadApprovedAt = null;
            task.UpdatedAt = DateTime.UtcNow;

            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Review rejected; task set back to In Progress.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        // If lead rejects, only allow if lead had approved or status is ReviewRequested - revert to InProgress
        if (isLead)
        {
            task.Status = "InProgress";
            task.ReviewRequestedById = null;
            task.ReviewRequestedAt = null;
            task.LeadApprovedById = null;
            task.LeadApprovedAt = null;
            task.UpdatedAt = DateTime.UtcNow;

            _context.Tasks.Update(task);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Lead rejected review; task set back to In Progress.";
            return RedirectToAction("Details", new { id = task.Id });
        }

        return Forbid();
    }
}
