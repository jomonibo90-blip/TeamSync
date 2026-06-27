using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                Description = g.Description ?? string.Empty,
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
                .Where(gm => gm.UserId == user.Id && gm.Group != null)
                .OrderByDescending(gm => gm.Group!.CreatedAt)
                .ToListAsync();

            groupViewModels = memberships.Select(gm => new GroupListViewModel
            {
                Id = gm.Group!.Id,
                Name = gm.Group.Name,
                Description = gm.Group.Description ?? string.Empty,
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

        // Load pending removal requests for this group
        var pendingRequests = await _context.RemovalRequests
            .Where(rr => rr.GroupId == id && rr.Status == "Pending")
            .Include(rr => rr.User)
            .Include(rr => rr.RequestedBy)
            .ToListAsync();

        // Load pending add member requests for this group
        var pendingAddRequests = await _context.AddMemberRequests
            .Where(amr => amr.GroupId == id && amr.Status == "Pending")
            .Include(amr => amr.RequestedBy)
            .ToListAsync();

        // Load pending join requests for this group
        var pendingJoinRequests = await _context.JoinRequests
            .Where(jr => jr.GroupId == id && jr.Status == "Pending")
            .Include(jr => jr.User)
            .ToListAsync();

        var viewModel = new GroupDetailsViewModel
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description ?? string.Empty,
            JoinCode = group.JoinCode,
            IsActive = group.IsActive,
            ArchivedAt = group.ArchivedAt,
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
            }).ToList(),
            PendingRemovalRequests = pendingRequests.Select(rr => new RemovalRequestViewModel
            {
                Id = rr.Id,
                UserFullName = $"{rr.User?.FirstName} {rr.User?.LastName}",
                RequestedByFullName = $"{rr.RequestedBy?.FirstName} {rr.RequestedBy?.LastName}",
                Reason = rr.Reason,
                Status = rr.Status,
                CreatedAt = rr.CreatedAt,
                RequestType = rr.UserId == rr.RequestedByUserId ? "Leave" : "Removal"
            }).ToList(),
            PendingAddRequests = pendingAddRequests.Select(amr => new AddMemberRequestViewModel
            {
                Id = amr.Id,
                Email = amr.Email,
                RequestedByFullName = $"{amr.RequestedBy?.FirstName} {amr.RequestedBy?.LastName}",
                Status = amr.Status,
                CreatedAt = amr.CreatedAt
            }).ToList(),
            PendingJoinRequests = pendingJoinRequests.Select(jr => new JoinRequestViewModel
            {
                Id = jr.Id,
                UserFullName = $"{jr.User?.FirstName} {jr.User?.LastName}",
                Email = jr.User?.Email ?? string.Empty,
                Status = jr.Status,
                CreatedAt = jr.CreatedAt
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

        // Check if a pending join request already exists
        var existingRequest = await _context.JoinRequests
            .FirstOrDefaultAsync(jr => jr.GroupId == group.Id && jr.UserId == user.Id && jr.Status == "Pending");

        if (existingRequest != null)
        {
            ModelState.AddModelError(string.Empty, "You already have a pending join request for this group.");
            return View(model);
        }

        bool isProfessor = await _userManager.IsInRoleAsync(user, "Professor");

        // Professors can join directly, students need approval
        if (isProfessor)
        {
            var groupMember = new GroupMember
            {
                GroupId = group.Id,
                UserId = user.Id,
                Role = "Professor",
                JoinedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.GroupMembers.Add(groupMember);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Details), new { id = group.Id });
        }
        else
        {
            // Create a join request for professor approval
            var joinRequest = new JoinRequest
            {
                GroupId = group.Id,
                UserId = user.Id,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.JoinRequests.Add(joinRequest);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Join request sent to professor for approval.";
            return RedirectToAction("Index", "Groups");
        }
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

        if (!group.IsActive)
        {
            TempData["ErrorMessage"] = "This group is archived (project ended). You cannot add members.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

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

        // Check if a pending request already exists
        var existingRequest = await _context.AddMemberRequests
            .FirstOrDefaultAsync(amr => amr.GroupId == group.Id && amr.UserId == userToAdd.Id && amr.Status == "Pending");

        if (existingRequest != null)
        {
            TempData["ErrorMessage"] = "A pending add request for this user already exists.";
            return RedirectToAction(nameof(Details), new { id = model.GroupId });
        }

        bool isCurrentUserProfessor = currentMember?.Role == "Professor" || User.IsInRole("Admin");

        // If professor or admin, add directly. Otherwise, create a request
        if (isCurrentUserProfessor)
        {
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
        else
        {
            // Create an add member request for professor approval
            var request = new AddMemberRequest
            {
                GroupId = group.Id,
                UserId = userToAdd.Id,
                Email = userToAdd.Email,
                RequestedByUserId = currentUser.Id,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.AddMemberRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Member add request sent to professor for approval.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }
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

        if (!group.IsActive && group.ArchivedAt == null)
        {
            group.ArchivedAt = DateTime.UtcNow;
        }
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

        if (!group.IsActive && !User.IsInRole("Admin"))
        {
            TempData["ErrorMessage"] = "Cannot regenerate join code for an archived group.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveMember(int groupId, string userId, string reason = "")
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) return NotFound();

        // Prevent modifications on archived groups
        if (!group.IsActive)
        {
            TempData["ErrorMessage"] = "Cannot modify members in archived groups.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        var memberToRemove = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);

        if (memberToRemove == null)
        {
            TempData["ErrorMessage"] = "Member not found.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isLead = currentMember?.Role == "Lead";
        bool isRemovingSelf = userId == currentUser.Id;
        bool isCurrentUserProfessor = currentMember?.Role == "Professor";

        if (isAdmin || isCurrentUserProfessor)
        {
            // Direct removal (no approval needed)
            _context.GroupMembers.Remove(memberToRemove);
            await _context.SaveChangesAsync();

            // After removal, if no members remain, archive the group
            var remaining = await _context.GroupMembers.CountAsync(gm => gm.GroupId == groupId);
            if (remaining == 0)
            {
                group.IsActive = false;
                group.ArchivedAt = DateTime.UtcNow;
                _context.Groups.Update(group);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Group has been archived because it no longer has members.";
                return RedirectToAction("Index", "Home");
            }
            TempData["SuccessMessage"] = "Member removed successfully.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }
        else if (isLead || isRemovingSelf)
        {
            // Check if any professors exist in the group
            var hasProfessor = group.Members.Any(m => m.Role == "Professor");

            if (!hasProfessor && isRemovingSelf)
            {
                // No professor in group: auto-approve student's leave request
                _context.GroupMembers.Remove(memberToRemove);
                await _context.SaveChangesAsync();

                // Check if this was the last member
                var remaining = await _context.GroupMembers.CountAsync(gm => gm.GroupId == groupId);
                if (remaining == 0)
                {
                    group.IsActive = false;
                    group.ArchivedAt = DateTime.UtcNow;
                    _context.Groups.Update(group);
                    await _context.SaveChangesAsync();
                    TempData["SuccessMessage"] = "You have left the group. Group has been archived because it no longer has members.";
                    return RedirectToAction("Index", "Home");
                }

                TempData["SuccessMessage"] = "You have left the group successfully.";
                return RedirectToAction("Index", "Groups");
            }

            // Professor exists (or Lead trying to remove another): create removal request
            var removalRequest = new RemovalRequest
            {
                GroupMemberId = memberToRemove.Id,
                GroupId = groupId,
                UserId = userId,
                RequestedByUserId = currentUser.Id,
                Reason = reason,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            _context.RemovalRequests.Add(removalRequest);
            await _context.SaveChangesAsync();

            string message = isRemovingSelf 
                ? "Your leave request has been sent to the professor for approval." 
                : "Removal request has been sent to the professor for approval.";
            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Details), new { id = groupId });
        }
        else
        {
            TempData["ErrorMessage"] = "You don't have permission to remove members.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveMemberRemoval(int removalRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var removalRequest = await _context.RemovalRequests
            .Include(rr => rr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(rr => rr.Id == removalRequestId);

        if (removalRequest == null) return NotFound();

        if (removalRequest.Group == null) return NotFound();

        // Only professor of the group or admin can approve
        var currentMember = removalRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to approve this request.";
            return RedirectToAction(nameof(Details), new { id = removalRequest.GroupId });
        }

        // Approve and remove the member
        removalRequest.Status = "Approved";
        removalRequest.ApprovedByUserId = currentUser.Id;
        removalRequest.ResolvedAt = DateTime.UtcNow;

        var memberToRemove = await _context.GroupMembers
            .FirstOrDefaultAsync(m => m.Id == removalRequest.GroupMemberId);

        if (memberToRemove != null)
        {
            _context.GroupMembers.Remove(memberToRemove);
        }

        _context.RemovalRequests.Update(removalRequest);
        await _context.SaveChangesAsync();

        // After removal and save, archive group if it has no members
        var remainingAfter = await _context.GroupMembers.CountAsync(gm => gm.GroupId == removalRequest.GroupId);
        if (remainingAfter == 0)
        {
            var grp = removalRequest.Group;
            if (grp != null)
            {
                grp.IsActive = false;
                grp.ArchivedAt = DateTime.UtcNow;
                _context.Groups.Update(grp);
                await _context.SaveChangesAsync();
            }
            TempData["SuccessMessage"] = "Removal request approved. Member removed and group archived because it has no remaining members.";
            return RedirectToAction("Index", "Home");
        }

        TempData["SuccessMessage"] = "Member removed successfully.";
        return RedirectToAction(nameof(Details), new { id = removalRequest.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectMemberRemoval(int removalRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var removalRequest = await _context.RemovalRequests
            .Include(rr => rr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(rr => rr.Id == removalRequestId);

        if (removalRequest == null) return NotFound();

        if (removalRequest.Group == null) return NotFound();

        // Only professor of the group or admin can reject
        var currentMember = removalRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to reject this request.";
            return RedirectToAction(nameof(Details), new { id = removalRequest.GroupId });
        }

        // Reject the removal request
        removalRequest.Status = "Rejected";
        removalRequest.ApprovedByUserId = currentUser.Id;
        removalRequest.ResolvedAt = DateTime.UtcNow;

        _context.RemovalRequests.Update(removalRequest);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Removal request rejected.";
        return RedirectToAction(nameof(Details), new { id = removalRequest.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(int id)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        // Only a professor member of this group can archive it
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");
        bool isAdmin = User.IsInRole("Admin");

        // Professor must be a member AND have professor role, OR be admin
        bool canDelete = isAdmin || (isProfessor && currentMember?.Role == "Professor");

        if (!canDelete)
        {
            TempData["ErrorMessage"] = "You don't have permission to delete this group. Only professors who are members of this group can archive it.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        // Archive the group instead of hard delete (professors/admins only)
        group.IsActive = false;
        group.ArchivedAt = DateTime.UtcNow;
        _context.Groups.Update(group);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Group archived successfully. Administrators can purge permanently.";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PurgeGroup(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var group = await _context.Groups
            .Include(g => g.Members)
            .Include(g => g.Tasks)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        // Only allow purging archived groups
        if (group.IsActive)
        {
            TempData["ErrorMessage"] = "Cannot purge an active group. Archive it first.";
            return RedirectToAction(nameof(Details), new { id = group.Id });
        }

        try
        {
            // Delete in order to avoid cascade delete constraint violations
            // 1. Delete removal requests first (has NoAction on GroupMemberId)
            var removalRequests = await _context.RemovalRequests
                .Where(rr => rr.GroupId == id)
                .ToListAsync();
            _context.RemovalRequests.RemoveRange(removalRequests);

            // 2. Delete add member requests
            var addMemberRequests = await _context.AddMemberRequests
                .Where(amr => amr.GroupId == id)
                .ToListAsync();
            _context.AddMemberRequests.RemoveRange(addMemberRequests);

            // 3. Delete join requests
            var joinRequests = await _context.JoinRequests
                .Where(jr => jr.GroupId == id)
                .ToListAsync();
            _context.JoinRequests.RemoveRange(joinRequests);

            // 4. Save changes to clear request tables
            await _context.SaveChangesAsync();

            // 5. Delete contributions (related to tasks)
            var tasksInGroup = await _context.Tasks
                .Where(t => t.GroupId == id)
                .ToListAsync();

            foreach (var task in tasksInGroup)
            {
                var contributions = await _context.Contributions
                    .Where(c => c.TaskId == task.Id)
                    .ToListAsync();
                _context.Contributions.RemoveRange(contributions);
            }

            // 6. Save to clear contributions
            await _context.SaveChangesAsync();

            // 7. Delete group members
            var groupMembers = await _context.GroupMembers
                .Where(gm => gm.GroupId == id)
                .ToListAsync();
            _context.GroupMembers.RemoveRange(groupMembers);

            // 8. Save to clear group members
            await _context.SaveChangesAsync();

            // 9. Now remove the group
            _context.Groups.Remove(group);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Group permanently deleted.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error deleting group: {ex.Message}";
            return RedirectToAction(nameof(Details), new { id = id });
        }
    }

    [HttpGet]
    public async Task<IActionResult> ExportGroup(int id)
    {
        if (!User.IsInRole("Admin")) return Forbid();

        var group = await _context.Groups
            .Include(g => g.Members).ThenInclude(m => m.User)
            .Include(g => g.Tasks)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group == null) return NotFound();

        var sb = new StringBuilder();
        sb.AppendLine("GroupId,Name,Description,IsActive,ArchivedAt,CreatedAt");
        sb.AppendLine($"{group.Id},\"{group.Name}\",\"{group.Description ?? string.Empty}\",{group.IsActive},{group.ArchivedAt},{group.CreatedAt}");
        sb.AppendLine();
        sb.AppendLine("Members:");
        sb.AppendLine("UserId,FullName,Email,Role,JoinedAt");
        foreach (var m in group.Members)
        {
            sb.AppendLine($"{m.UserId},\"{m.User?.FirstName} {m.User?.LastName}\",{m.User?.Email},{m.Role},{m.JoinedAt}");
        }
        sb.AppendLine();
        sb.AppendLine("Tasks:");
        sb.AppendLine("TaskId,Title,Description,AssignedToId,Status,CreatedAt");
        foreach (var t in group.Tasks)
        {
            sb.AppendLine($"{t.Id},\"{t.Title}\",\"{t.Description ?? string.Empty}\",{t.AssignedToId},{t.Status},{t.CreatedAt}");
        }
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"group-{group.Id}.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PromoteToLead(int groupId, string userId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var group = await _context.Groups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == groupId);

        if (group == null) return NotFound();

        // Prevent modifications on archived groups
        if (!group.IsActive)
        {
            TempData["ErrorMessage"] = "Cannot modify member roles in archived groups.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        // Only professor or admin can promote to lead
        var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to promote members.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        var memberToPromote = group.Members.FirstOrDefault(m => m.UserId == userId);
        if (memberToPromote == null)
        {
            TempData["ErrorMessage"] = "Member not found.";
            return RedirectToAction(nameof(Details), new { id = groupId });
        }

        memberToPromote.Role = "Lead";
        _context.GroupMembers.Update(memberToPromote);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Member promoted to Lead successfully.";
        return RedirectToAction(nameof(Details), new { id = groupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveAddMember(int addMemberRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var addMemberRequest = await _context.AddMemberRequests
            .Include(amr => amr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(amr => amr.Id == addMemberRequestId);

        if (addMemberRequest == null) return NotFound();

        if (addMemberRequest.Group == null) return NotFound();

        // Only professor of the group or admin can approve
        var currentMember = addMemberRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to approve this request.";
            return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
        }

        // Get the user to add
        var userToAdd = await _userManager.FindByIdAsync(addMemberRequest.UserId);
        if (userToAdd == null)
        {
            // User no longer exists, reject the request
            addMemberRequest.Status = "Rejected";
            addMemberRequest.ApprovedByUserId = currentUser.Id;
            addMemberRequest.ResolvedAt = DateTime.UtcNow;
            _context.AddMemberRequests.Update(addMemberRequest);
            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "User no longer exists.";
            return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
        }

        // Check if user is already a member
        if (addMemberRequest.Group.Members.Any(m => m.UserId == userToAdd.Id))
        {
            addMemberRequest.Status = "Rejected";
            addMemberRequest.ApprovedByUserId = currentUser.Id;
            addMemberRequest.ResolvedAt = DateTime.UtcNow;
            _context.AddMemberRequests.Update(addMemberRequest);
            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "User is already a member of this group.";
            return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
        }

        // Approve and add the member
        addMemberRequest.Status = "Approved";
        addMemberRequest.ApprovedByUserId = currentUser.Id;
        addMemberRequest.ResolvedAt = DateTime.UtcNow;

        bool isUserProfessor = await _userManager.IsInRoleAsync(userToAdd, "Professor");

        var groupMember = new GroupMember
        {
            GroupId = addMemberRequest.GroupId,
            UserId = userToAdd.Id,
            Role = isUserProfessor ? "Professor" : "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.AddMemberRequests.Update(addMemberRequest);
        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Member add request approved. User has been added to the group.";
        return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectAddMember(int addMemberRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var addMemberRequest = await _context.AddMemberRequests
            .Include(amr => amr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(amr => amr.Id == addMemberRequestId);

        if (addMemberRequest == null) return NotFound();

        if (addMemberRequest.Group == null) return NotFound();

        // Only professor of the group or admin can reject
        var currentMember = addMemberRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to reject this request.";
            return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
        }

        // Reject the add member request
        addMemberRequest.Status = "Rejected";
        addMemberRequest.ApprovedByUserId = currentUser.Id;
        addMemberRequest.ResolvedAt = DateTime.UtcNow;

        _context.AddMemberRequests.Update(addMemberRequest);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Member add request rejected.";
        return RedirectToAction(nameof(Details), new { id = addMemberRequest.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveJoinRequest(int joinRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var joinRequest = await _context.JoinRequests
            .Include(jr => jr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(jr => jr.Id == joinRequestId);

        if (joinRequest == null) return NotFound();

        if (joinRequest.Group == null) return NotFound();

        // Only professor of the group or admin can approve
        var currentMember = joinRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to approve this request.";
            return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
        }

        // Get the user trying to join
        var userToJoin = await _userManager.FindByIdAsync(joinRequest.UserId);
        if (userToJoin == null)
        {
            joinRequest.Status = "Rejected";
            joinRequest.ApprovedByUserId = currentUser.Id;
            joinRequest.ResolvedAt = DateTime.UtcNow;
            _context.JoinRequests.Update(joinRequest);
            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "User no longer exists.";
            return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
        }

        // Check if user is already a member
        if (joinRequest.Group.Members.Any(m => m.UserId == userToJoin.Id))
        {
            joinRequest.Status = "Rejected";
            joinRequest.ApprovedByUserId = currentUser.Id;
            joinRequest.ResolvedAt = DateTime.UtcNow;
            _context.JoinRequests.Update(joinRequest);
            await _context.SaveChangesAsync();

            TempData["ErrorMessage"] = "User is already a member of this group.";
            return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
        }

        // Approve and add the member
        joinRequest.Status = "Approved";
        joinRequest.ApprovedByUserId = currentUser.Id;
        joinRequest.ResolvedAt = DateTime.UtcNow;

        var groupMember = new GroupMember
        {
            GroupId = joinRequest.GroupId,
            UserId = userToJoin.Id,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        _context.JoinRequests.Update(joinRequest);
        _context.GroupMembers.Add(groupMember);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Join request approved. User has been added to the group.";
        return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectJoinRequest(int joinRequestId)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null) return Challenge();

        var joinRequest = await _context.JoinRequests
            .Include(jr => jr.Group)
            .ThenInclude(g => g.Members)
            .FirstOrDefaultAsync(jr => jr.Id == joinRequestId);

        if (joinRequest == null) return NotFound();

        if (joinRequest.Group == null) return NotFound();

        // Only professor of the group or admin can reject
        var currentMember = joinRequest.Group.Members
            .FirstOrDefault(m => m.UserId == currentUser.Id);

        bool isAdmin = User.IsInRole("Admin");
        bool isProfessor = currentMember?.Role == "Professor" || User.IsInRole("Professor");

        if (!isAdmin && !isProfessor)
        {
            TempData["ErrorMessage"] = "You don't have permission to reject this request.";
            return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
        }

        // Reject the join request
        joinRequest.Status = "Rejected";
        joinRequest.ApprovedByUserId = currentUser.Id;
        joinRequest.ResolvedAt = DateTime.UtcNow;

        _context.JoinRequests.Update(joinRequest);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = "Join request rejected.";
        return RedirectToAction(nameof(Details), new { id = joinRequest.GroupId });
    }
}
