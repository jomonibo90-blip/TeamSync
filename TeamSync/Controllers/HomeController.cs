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
            var groupIds = memberships.Where(m => m.Group != null).Select(m => m.Group!.Id).ToList();
            
            var allTasks = await _context.Tasks
                .Include(t => t.Group)
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

            // Calculate total hours considering overrides
            decimal totalHours = 0m;
            foreach (var contribution in contributionsList)
            {
                totalHours += await GetFinalHoursAsync(contribution);
            }

            var contributionsCount = contributionsList.Count;

            // Calculate weekly contribution score (0-10 scale)
            var oneWeekAgo = DateTime.UtcNow.AddDays(-7);
            var weeklyContributions = contributionsList
                .Where(c => c.ContributedAt >= oneWeekAgo)
                .ToList();

            // Calculate final hours for weekly contributions (considering overrides)
            decimal weeklyFinalHours = 0m;
            foreach (var contribution in weeklyContributions)
            {
                weeklyFinalHours += await GetFinalHoursAsync(contribution);
            }

            decimal weeklyScore = CalculateWeeklyScore(weeklyContributions.Count, weeklyFinalHours, inProgressTasks, pendingTasks);

            // Get upcoming tasks (next 7 days)
            var sevenDaysFromNow = DateTime.UtcNow.AddDays(7);
            var upcomingTasks = allTasks
                .Where(t => t.DueDate.HasValue && t.DueDate.Value >= DateTime.UtcNow && t.DueDate.Value <= sevenDaysFromNow && t.Status != "Completed")
                .OrderBy(t => t.DueDate)
                .Take(5)
                .Select(t => new UpcomingTaskViewModel
                {
                    TaskId = t.Id,
                    TaskTitle = t.Title,
                    GroupName = t.Group?.Name ?? "Unknown",
                    DueDate = t.DueDate,
                    Status = t.Status,
                    DaysUntilDue = t.DueDate.HasValue ? (int)(t.DueDate.Value.Date - DateTime.UtcNow.Date).TotalDays : 0
                })
                .ToList();

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

            // Get recent alerts/notifications
            var recentAlerts = await _context.Notifications
                .Where(n => n.UserId == user.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(5)
                .Select(n => new NotificationViewModel
                {
                    Id = n.Id,
                    Type = n.Type,
                    Message = n.Message,
                    CreatedAt = n.CreatedAt,
                    IsRead = n.IsRead,
                    TaskId = n.TaskId
                })
                .ToListAsync();

            var studentViewModel = new StudentDashboardViewModel
            {
                Groups = groupViewModels,
                RecentAlerts = recentAlerts,
                Progress = new StudentProgressViewModel
                {
                    TotalTasks = totalTasks,
                    CompletedTasks = completedTasks,
                    InProgressTasks = inProgressTasks,
                    PendingTasks = pendingTasks,
                    TotalHoursContributed = totalHours,
                    ContributionsCount = contributionsCount,
                    WeeklyContributionScore = weeklyScore,
                    UpcomingTasks = upcomingTasks,
                    GroupProgress = groupProgress
                }
            };

            return View("StudentDashboard", studentViewModel);
        }
    }

    /// <summary>
    /// Gets the final hours for a contribution, considering any overrides.
    /// If a ContributionOverride exists, returns NewHours; otherwise returns original HoursSpent.
    /// </summary>
    private async Task<decimal> GetFinalHoursAsync(Contribution contribution)
    {
        if (contribution?.Id == 0)
            return contribution?.HoursSpent ?? 0m;

        // Check if there's an override for this contribution
        var overrideRecord = await _context.ContributionOverrides
            .Where(co => co.ContributionId == contribution.Id)
            .OrderByDescending(co => co.OverriddenAt)
            .FirstOrDefaultAsync();

        if (overrideRecord?.NewHours.HasValue == true)
        {
            return overrideRecord.NewHours.Value;
        }

        return contribution?.HoursSpent ?? 0m;
    }

    private decimal CalculateWeeklyScore(int contributionCount, decimal weeklyFinalHours, int inProgressTasks, int pendingTasks)
    {
        // Score calculation:
        // Base: 5 points
        // + 0.5 points per contribution (up to 2.5 points max)
        // + 0.1 per hour of final contributed hours (up to 1.5 points max)
        // - 0.5 per pending/overdue task

        var baseScore = 5m;
        var contributionBonus = Math.Min(contributionCount * 0.5m, 2.5m);
        var hoursBonus = Math.Min(weeklyFinalHours * 0.1m, 1.5m);
        var pendingPenalty = (inProgressTasks + pendingTasks) * 0.5m;

        var score = baseScore + contributionBonus + hoursBonus - pendingPenalty;
        return Math.Max(0, Math.Min(10, score)); // Clamp between 0-10
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
