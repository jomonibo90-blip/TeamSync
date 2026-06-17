using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public AdminController(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task<IActionResult> Dashboard()
    {
        var totalUsers = await _userManager.Users.CountAsync();
        var professors = await _userManager.GetUsersInRoleAsync("Professor");
        var students = await _userManager.GetUsersInRoleAsync("Student");
        var groups = await _context.Groups.CountAsync();
        var activeGroups = await _context.Groups.CountAsync(g => g.IsActive);

        var viewModel = new AdminDashboardViewModel
        {
            TotalUsers = totalUsers,
            TotalProfessors = professors.Count,
            TotalStudents = students.Count,
            TotalGroups = groups,
            ActiveGroups = activeGroups
        };

        return View(viewModel);
    }

    public async Task<IActionResult> Users(string role = "", string search = "")
    {
        var query = _userManager.Users.AsQueryable();

        // Filter by role if specified
        if (!string.IsNullOrEmpty(role) && role != "All")
        {
            var usersInRole = await _userManager.GetUsersInRoleAsync(role);
            var userIds = usersInRole.Select(u => u.Id).ToList();
            query = query.Where(u => userIds.Contains(u.Id));
        }

        // Search by name or email
        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(search) ||
                u.LastName.ToLower().Contains(search) ||
                u.Email.ToLower().Contains(search) ||
                u.StudentId.ToLower().Contains(search));
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        var userViewModels = new List<UserListViewModel>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var groupCount = await _context.GroupMembers
                .CountAsync(gm => gm.UserId == user.Id);

            userViewModels.Add(new UserListViewModel
            {
                Id = user.Id,
                FullName = $"{user.FirstName} {user.LastName}",
                Email = user.Email ?? string.Empty,
                StudentId = user.StudentId,
                Role = roles.FirstOrDefault() ?? "No Role",
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                GroupCount = groupCount
            });
        }

        ViewData["CurrentRole"] = role;
        ViewData["SearchTerm"] = search;
        return View(userViewModels);
    }

    [HttpGet]
    public IActionResult Enroll()
    {
        return View(new EnrollUserViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Enroll(EnrollUserViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(model.Email);
        if (existingUser != null)
        {
            ModelState.AddModelError("Email", "A user with this email already exists.");
            return View(model);
        }

        // Create new user
        var user = new User
        {
            UserName = model.Email,
            Email = model.Email,
            FirstName = model.FirstName,
            LastName = model.LastName,
            StudentId = model.StudentId,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            EmailConfirmed = true // Admin enrolls confirmed users
        };

        // Create user with password
        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // Assign role
        var roleExists = await _roleManager.RoleExistsAsync(model.Role);
        if (roleExists)
        {
            await _userManager.AddToRoleAsync(user, model.Role);
        }

        TempData["SuccessMessage"] = $"User {model.FirstName} {model.LastName} enrolled successfully as {model.Role}.";
        return RedirectToAction(nameof(Users));
    }

    [HttpGet]
    public async Task<IActionResult> ManageUser(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        var roles = await _userManager.GetRolesAsync(user);
        var userGroups = await _context.GroupMembers
            .Where(gm => gm.UserId == id)
            .Include(gm => gm.Group)
            .Select(gm => new UserGroupViewModel
            {
                GroupId = gm.Group.Id,
                GroupName = gm.Group.Name,
                Role = gm.Role,
                JoinedAt = gm.JoinedAt
            })
            .ToListAsync();

        var viewModel = new ManageUserViewModel
        {
            Id = user.Id,
            FullName = $"{user.FirstName} {user.LastName}",
            Email = user.Email ?? string.Empty,
            StudentId = user.StudentId,
            Role = roles.FirstOrDefault() ?? "No Role",
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            Groups = userGroups
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeactivateUser(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        // Prevent deactivating yourself
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser?.Id == id)
        {
            TempData["ErrorMessage"] = "You cannot deactivate your own account.";
            return RedirectToAction(nameof(ManageUser), new { id });
        }

        user.IsActive = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        TempData["SuccessMessage"] = $"User {user.FirstName} {user.LastName} has been deactivated.";
        return RedirectToAction(nameof(ManageUser), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReactivateUser(string id)
    {
        if (string.IsNullOrEmpty(id))
            return NotFound();

        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
            return NotFound();

        user.IsActive = true;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        TempData["SuccessMessage"] = $"User {user.FirstName} {user.LastName} has been reactivated.";
        return RedirectToAction(nameof(ManageUser), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveUserFromGroup(string userId, int groupId)
    {
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.UserId == userId && gm.GroupId == groupId);

        if (membership == null)
            return NotFound();

        _context.GroupMembers.Remove(membership);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "User removed from group successfully.";
        return RedirectToAction(nameof(ManageUser), new { id = userId });
    }
}
