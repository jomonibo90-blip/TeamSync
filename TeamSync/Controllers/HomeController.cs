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

            return View("StudentDashboard", groupViewModels);
        }
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
