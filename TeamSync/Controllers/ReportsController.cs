using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;

namespace TeamSync.Controllers;

[Authorize(Roles = "Professor")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly ILogger<ReportsController> _logger;

    public ReportsController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        ILogger<ReportsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Forbid();

            // Get groups where this professor is assigned as an Instructor
            // (Note: Only Leads can create groups. Professors are assigned to groups for oversight)
            var professorGroups = await _context.Groups
                .Where(g => g.Members.Any(gm => gm.UserId == currentUser.Id && gm.Role == "Instructor"))
                .Include(g => g.Members)
                .ThenInclude(gm => gm.User)
                .Include(g => g.Tasks)
                .ToListAsync();

            var reportData = new
            {
                TotalGroups = professorGroups.Count,
                TotalStudents = professorGroups.SelectMany(g => g.Members).Count(),
                TotalTasks = professorGroups.SelectMany(g => g.Tasks).Count(),
                CompletedTasks = professorGroups.SelectMany(g => g.Tasks)
                    .Where(t => t.Status == "Completed").Count(),
                Groups = professorGroups.Select(g => new
                {
                    g.Id,
                    g.Name,
                    MemberCount = g.Members.Count,
                    TaskCount = g.Tasks.Count,
                    CompletedTaskCount = g.Tasks.Where(t => t.Status == "Completed").Count(),
                    UpdatedAt = g.UpdatedAt
                })
            };

            return View(reportData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading reports");
            TempData["ErrorMessage"] = "An error occurred while loading reports.";
            return RedirectToAction("Index", "Home");
        }
    }

    public async Task<IActionResult> GroupDetails(int id)
    {
        try
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return Forbid();

            // Verify professor is assigned to this group as Instructor for oversight
            // (Note: Only Leads can create groups. Professors are assigned for monitoring and support)
            var group = await _context.Groups
                .Where(g => g.Id == id && g.Members.Any(gm => gm.UserId == currentUser.Id && gm.Role == "Instructor"))
                .Include(g => g.Members)
                .ThenInclude(gm => gm.User)
                .Include(g => g.Tasks)
                .FirstOrDefaultAsync();

            if (group == null)
                return NotFound();

            var groupReport = new
            {
                group.Id,
                group.Name,
                group.Description,
                group.CreatedAt,
                group.UpdatedAt,
                MemberCount = group.Members.Count,
                Members = group.Members.Select(gm => new
                {
                    gm.User?.Id,
                    gm.User?.FirstName,
                    gm.User?.LastName,
                    gm.User?.Email,
                    gm.Role,
                    gm.JoinedAt
                }),
                TaskCount = group.Tasks.Count,
                Tasks = group.Tasks.Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Status,
                    t.Priority,
                    t.CreatedAt,
                    t.DueDate,
                    t.UpdatedAt
                })
            };

            return View(groupReport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading group report for group {GroupId}", id);
            TempData["ErrorMessage"] = "An error occurred while loading the group report.";
            return RedirectToAction(nameof(Index));
        }
    }
}
