using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;

namespace TeamSync.Controllers;

[Authorize]
public class GroupsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;

    public GroupsController(ApplicationDbContext context, UserManager<User> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var isAdmin = User.IsInRole("Admin");

        List<GroupListViewModel> groupViewModels;

        if (isAdmin)
        {
            // Admins see all groups
            var groups = await _context.Groups
                .Include(g => g.Members)
                .OrderByDescending(g => g.CreatedAt)
                .ToListAsync();

            groupViewModels = groups.Select(g => new GroupListViewModel
            {
                Id = g.Id,
                Name = g.Name,
                Description = g.Description,
                MemberCount = g.Members.Count,
                CreatedAt = g.CreatedAt,
                IsActive = g.IsActive,
                UserRole = g.CreatedById == user.Id ? "Professor" : "Admin"
            }).ToList();
        }
        else
        {
            // Professors and Students only see groups they are a member of
            var memberships = await _context.GroupMembers
                .Include(gm => gm.Group)
                .ThenInclude(g => g.Members)
                .Where(gm => gm.UserId == user.Id)
                .OrderByDescending(gm => gm.Group.CreatedAt)
                .ToListAsync();

            groupViewModels = memberships.Select(gm => new GroupListViewModel
            {
                Id = gm.Group.Id,
                Name = gm.Group.Name,
                Description = gm.Group.Description,
                MemberCount = gm.Group.Members.Count,
                CreatedAt = gm.Group.CreatedAt,
                IsActive = gm.Group.IsActive,
                UserRole = gm.Role
            }).ToList();
        }

        return View(groupViewModels);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateGroupViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var joinCode = GenerateUniqueJoinCode();

        var group = new Group
        {
            Name = model.Name,
            Description = model.Description,
            CreatedById = user.Id,
            JoinCode = joinCode,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync(); // Save to get the Group Id

        // Add creator as a member
        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = user.Id,
            Role = User.IsInRole("Professor") || User.IsInRole("Admin") ? "Professor" : "Lead",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = group.Id });
    }

    private string GenerateUniqueJoinCode()
    {
        // Simple code generator: First 3 letters of Guid, followed by random number
        var random = new Random();
        return $"{Guid.NewGuid().ToString().Substring(0, 3).ToUpper()}-{random.Next(100, 999)}";
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == user.Id);

        // Admins can see everything. Professors/Students must be a member.
        bool isObserver = User.IsInRole("Admin");
        if (currentMember == null && !isObserver)
        {
            return Forbid();
        }

        var viewModel = new GroupDetailsViewModel
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description ?? string.Empty,
            JoinCode = group.JoinCode,
            IsActive = group.IsActive,
            CreatedAt = group.CreatedAt,
            CurrentUserRole = currentMember?.Role ?? "Admin",
            Members = group.Members
                .OrderByDescending(m => m.Role == "Professor") // Professors first
                .ThenByDescending(m => m.Role == "Lead")       // Then Leads
                .ThenBy(m => m.JoinedAt)                       // Then chronologically
                .Select(m => new GroupMemberViewModel
            {
                UserId = m.UserId,
                FullName = $"{m.User?.FirstName} {m.User?.LastName}",
                Email = m.User?.Email ?? string.Empty,
                Role = m.Role,
                JoinedAt = m.JoinedAt
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Join()
    {
        return View(new JoinGroupViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(JoinGroupViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var cleanJoinCode = model.JoinCode.Replace("-", "").ToUpper();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.JoinCode.Replace("-", "").ToUpper() == cleanJoinCode && g.IsActive);

        if (group == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid or expired Join Code.");
            return View(model);
        }

        if (group.Members.Any(m => m.UserId == user.Id))
        {
            ModelState.AddModelError(string.Empty, "You are already a member of this group.");
            return View(model);
        }

        bool isProfessor = await _userManager.IsInRoleAsync(user, "Professor");

        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = user.Id,
            Role = isProfessor ? "Professor" : "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Details), new { id = group.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMember(AddMemberViewModel model)
    {
        if (!ModelState.IsValid)
        {
            TempData["ErrorMessage"] = "Invalid email format.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }

        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == model.GroupId);

        if (group == null) return NotFound();

        // Ensure the current user has permission to add members
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool hasPermission = currentMember?.Role == "Lead" || currentMember?.Role == "Professor" || User.IsInRole("Admin");

        if (!hasPermission)
        {
            TempData["ErrorMessage"] = "You don't have permission to add members to this group.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }

        var userToAdd = await _userManager.FindByEmailAsync(model.Email);
        if (userToAdd == null)
        {
            TempData["ErrorMessage"] = "User with that email not found.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }

        if (group.Members.Any(m => m.UserId == userToAdd.Id))
        {
            TempData["ErrorMessage"] = "User is already a member of this group.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }

        bool isUserToAddProfessor = await _userManager.IsInRoleAsync(userToAdd, "Professor");

        var groupMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = userToAdd.Id,
            Role = isUserToAddProfessor ? "Professor" : "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Member added successfully.";
        return RedirectToAction(nameof(Details), new { id = group.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == user.Id);
        bool canEdit = currentMember?.Role == "Professor" || currentMember?.Role == "Lead" || User.IsInRole("Admin");

        if (!canEdit)
        {
            return Forbid();
        }

        var viewModel = new EditGroupViewModel
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description ?? string.Empty,
            IsActive = group.IsActive
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditGroupViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == model.Id);

        if (group == null) return NotFound();

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == user.Id);
        bool canEdit = currentMember?.Role == "Professor" || currentMember?.Role == "Lead" || User.IsInRole("Admin");

        if (!canEdit)
        {
            return Forbid();
        }

        group.Name = model.Name;
        group.Description = model.Description;
        group.IsActive = model.IsActive;
        group.UpdatedAt = DateTime.UtcNow;

        _context.Groups.Update(group);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Group updated successfully.";
        return RedirectToAction(nameof(Details), new { id = group.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateJoinCode(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == user.Id);
        bool canEdit = currentMember?.Role == "Professor" || currentMember?.Role == "Lead" || User.IsInRole("Admin");

        if (!canEdit)
        {
            return Forbid();
        }

        group.JoinCode = GenerateUniqueJoinCode();
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Join code regenerated successfully.";
        return RedirectToAction(nameof(Details), new { id = group.Id });
    }
}