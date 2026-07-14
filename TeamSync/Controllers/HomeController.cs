using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public HomeController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Dashboard");
        }

        // Redirect root to the login page for unauthenticated users
        return RedirectToAction("Login", "Account");
    }

    [Authorize]
    public async Task<IActionResult> Dashboard()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var isAdmin = User.IsInRole("Admin");
        var isProfessor = User.IsInRole("Professor");

        List<GroupListViewModel> groupViewModels;

        if (isAdmin)
        {
            var groups = await _context.Groups
                .Include(g => g.Members)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            groupViewModels = groups.Select(g => new GroupListViewModel
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description ?? string.Empty,
                MemberCount = g.Members.Count,
                StudentCount = g.Members.Count(m => m.Role != "Professor"),
                CreatedAt = g.CreatedAt,
                IsActive = g.IsActive,
                UserRole = g.CreatedById == user.Id ? "Professor" : "Admin"
            }).ToList();

            return View("AdminDashboard", groupViewModels);
        }
        else if (isProfessor)
        {
            var memberships = await _context.GroupMembers
                .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                .Where(gm => gm.UserId == user.Id && gm.Group != null)
                .OrderByDescending(gm => gm.Group!.CreatedAt)
                .ToListAsync();

            groupViewModels = memberships.Select(gm => new GroupListViewModel
            {
                Id = gm.Group!.Id,
                Name = gm.Group.Name,
                Description = gm.Group.Description ?? string.Empty,
                MemberCount = gm.Group.Members.Count,
                StudentCount = gm.Group.Members.Count(m => m.Role != "Professor"),
                CreatedAt = gm.Group.CreatedAt,
                IsActive = gm.Group.IsActive,
                UserRole = gm.Role
            }).ToList();

            return View("ProfessorDashboard", groupViewModels);
        }
        else
        {
            // Student Dashboard with Progress Tracking
            var memberships = await _context.GroupMembers
                .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                .Where(gm => gm.UserId == user.Id && gm.Group != null)
                .OrderByDescending(gm => gm.Group!.CreatedAt)
                .ToListAsync();

            groupViewModels = memberships.Select(gm => new GroupListViewModel
            {
                Id = gm.Group!.Id,
                Name = gm.Group.Name,
                Description = gm.Group.Description ?? string.Empty,
                MemberCount = gm.Group.Members.Count,
                StudentCount = gm.Group.Members.Count(m => m.Role != "Professor"),
                CreatedAt = gm.Group.CreatedAt,
                IsActive = gm.Group.IsActive,
                UserRole = gm.Role
            }).ToList();

            // Get all tasks assigned to this student
            var groupIds = memberships.Select(m => m.Group?.Id).Where(id => id.HasValue).Select(id => id.Value).ToList();
            
            var allTasks = await _context.Tasks
                .Where(t => t.GroupId.HasValue && groupIds.Contains(t.GroupId.Value) && t.AssignedToId == user.Id)
                .ToListAsync();

            var totalTasks = allTasks.Count;
            var completedTasks = allTasks.Count(t => t.Status == "Completed");
            var inProgressTasks = allTasks.Count(t => t.Status == "InProgress");
            var pendingTasks = allTasks.Count(t => t.Status == "Pending");

            // Contributions by this user for tasks in these groups
            var contributionsQuery = _context.Contributions
                .Include(c => c.Task)
                .Where(c => c.UserId == user.Id && c.Task != null && c.Task.GroupId.HasValue && groupIds.Contains(c.Task.GroupId.Value));

            var contributionsList = await contributionsQuery.ToListAsync();
            var totalHours = contributionsList.Where(c => c.HoursSpent.HasValue).Sum(c => c.HoursSpent.Value);
            var contributionsCount = contributionsList.Count;

            // Build per-group progress
            var groupProgress = new Dictionary<int, GroupProgressViewModel>();
            foreach (var membership in memberships)
            {
                var group = membership.Group;
                if (group == null) continue;

                var groupTasks = allTasks.Where(t => t.GroupId == group.Id).ToList();
                groupProgress[group.Id] = new GroupProgressViewModel
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    Total = groupTasks.Count,
                    Completed = groupTasks.Count(t => t.Status == "Completed"),
                    InProgress = groupTasks.Count(t => t.Status == "InProgress"),
                    Pending = groupTasks.Count(t => t.Status == "Pending")
                };
            }

            var studentViewModel = new StudentDashboardViewModel
            {
                Groups = groupViewModels,
                Progress = new StudentProgressViewModel
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    PendingTasks = pendingTasks,
                    TotalHoursContributed = totalHours,
                    ContributionsCount = contributionsCount,
                    GroupProgress = groupProgress
                }
            };

            return View("StudentDashboard", studentViewModel);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
