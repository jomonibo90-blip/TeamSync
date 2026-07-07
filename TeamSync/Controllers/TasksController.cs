using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

[Authorize]
public class TasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public TasksController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Create(int groupId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

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
        if (!ModelState.IsValid) return View(model);

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == model.GroupId);
        if (group == null) return NotFound();
        if (!group.IsActive) return BadRequest("Cannot add tasks to an archived group.");

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLead)
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

        var task = new TeamSync.Models.Task
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

        var task = new TeamSync.Models.Task
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
    public async Task<IActionResult> ApproveRequest(int taskId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var task = await _context.Tasks
            .Include(t => t.Group).ThenInclude(g => g.Members)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .FirstOrDefaultAsync(t => t.Id == taskId);

        if (task == null) return NotFound();
        if (!task.Group.IsActive) return BadRequest("Cannot modify tasks for an archived group.");

        var group = task.Group;
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLead)
            return Forbid();

        if (task.Status != "Requested")
        {
            TempData["ErrorMessage"] = "Task is not in requested state.";
            return RedirectToAction("Details", "Groups", new { id = task.GroupId });
        }

        task.Status = "Pending";
        task.UpdatedAt = DateTime.UtcNow;
        _context.Tasks.Update(task);
        await _context.SaveChangesAsync();

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
        if (!task.Group.IsActive) return BadRequest("Cannot modify tasks for an archived group.");

        var group = task.Group;
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isLead = currentMember?.Role == "Lead";

        if (!isAdmin && !isProfessor && !isLead)
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
}
