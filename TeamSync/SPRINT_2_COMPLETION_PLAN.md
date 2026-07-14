# Sprint 2 Completion Action Plan

**Status:** Ready for Implementation  
**Target:** Complete remaining Sprint 2 requirements  
**Priority:** High (Copilot Instructions compliance)

---

## Issue 1: Progress Bar UI Missing on Student Dashboard

### Current State
- Student Dashboard shows placeholder "Tasks Completed: 0"
- No real data integration
- Fake chart bars for UI purposes only
- No progress calculation logic

### Required Implementation

#### Step 1: Update StudentDashboardViewModel
**File:** `TeamSync/ViewModels/AdminViewModels.cs` or create new `StudentDashboardViewModel.cs`

```csharp
public class StudentDashboardViewModel
{
    public List<GroupListViewModel> Groups { get; set; } = new();
    
    // Progress tracking
    public int TotalTasks { get; set; }
    public int CompletedTasks { get; set; }
    public int InProgressTasks { get; set; }
    public int PendingTasks { get; set; }
    
    public decimal CompletionPercentage => TotalTasks > 0 
        ? (CompletedTasks * 100m) / TotalTasks 
        : 0;
    
    // Per-group progress
    public Dictionary<int, GroupProgress> GroupProgress { get; set; } = new();
}

public class GroupProgress
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public int Total { get; set; }
    public int Completed { get; set; }
    
    public decimal Percentage => Total > 0 
        ? (Completed * 100m) / Total 
        : 0;
}
```

#### Step 2: Update HomeController
**File:** `TeamSync/Controllers/HomeController.cs`

**Current Method Signature:**
```csharp
[Authorize(Roles = "Student")]
public async Task<IActionResult> StudentDashboard()
```

**Add this logic inside the method:**
```csharp
// Get all groups user is member of
var groups = await _context.GroupMembers
    .Include(gm => gm.Group)
    .Where(gm => gm.UserId == user.Id)
    .Select(gm => gm.Group)
    .ToListAsync();

var groupIds = groups.Select(g => g.Id).ToList();

// Get all tasks in these groups
var tasks = await _context.Tasks
    .Where(t => groupIds.Contains(t.GroupId.Value))
    .ToListAsync();

// Calculate totals
var totalTasks = tasks.Count;
var completedTasks = tasks.Count(t => t.Status == "Completed");
var inProgressTasks = tasks.Count(t => t.Status == "InProgress");
var pendingTasks = tasks.Count(t => t.Status == "Pending");

// Build group progress
var groupProgress = new Dictionary<int, GroupProgress>();
foreach (var group in groups)
{
    var groupTasks = tasks.Where(t => t.GroupId == group.Id).ToList();
    groupProgress[group.Id] = new GroupProgress
    {
        GroupId = group.Id,
        GroupName = group.Name,
        Total = groupTasks.Count,
        Completed = groupTasks.Count(t => t.Status == "Completed")
    };
}

var viewModel = new StudentDashboardViewModel
{
    Groups = groupViewModels,
    TotalTasks = totalTasks,
    CompletedTasks = completedTasks,
    InProgressTasks = inProgressTasks,
    PendingTasks = pendingTasks,
    GroupProgress = groupProgress
};

return View(viewModel);
```

#### Step 3: Update Student Dashboard View
**File:** `TeamSync/Views/Home/StudentDashboard.cshtml`

**Replace current Contribution Activity section with:**
```razor
<!-- Progress Overview Card -->
<div class="ts-dashboard-card">
    <div class="ts-dashboard-card-header">
        <h3 class="ts-title-lg">
            <span class="material-symbols-outlined ts-text-primary" style="font-size: 20px;">show_chart</span>
            Overall Progress
        </h3>
    </div>
    <div class="ts-dashboard-card-body">
        @if (Model.TotalTasks == 0)
        {
            <p class="ts-body-md ts-text-secondary">No tasks assigned yet.</p>
        }
        else
        {
            <!-- Progress Bar -->
            <div class="ts-mb-lg">
                <div class="ts-flex ts-justify-between ts-mb-sm">
                    <span class="ts-label-md">Overall Completion</span>
                    <span class="ts-label-md ts-text-primary">@Model.CompletionPercentage.ToString("F1")%</span>
                </div>
                <div style="height: 8px; background: var(--ts-surface-container-high); border-radius: 4px; overflow: hidden;">
                    <div style="height: 100%; width: @(Model.CompletionPercentage)%; background: linear-gradient(90deg, var(--ts-success), var(--ts-primary)); transition: width 0.3s ease; border-radius: 4px;"></div>
                </div>
            </div>

            <!-- Task Stats -->
            <div style="display: grid; grid-template-columns: repeat(2, 1fr); gap: 1rem;">
                <div style="padding: 1rem; background: var(--ts-surface-container-low); border-radius: var(--ts-radius-md); border-left: 3px solid var(--ts-success);">
                    <div class="ts-label-sm ts-text-secondary">Completed</div>
                    <div class="ts-display-md ts-text-success">@Model.CompletedTasks</div>
                </div>
                <div style="padding: 1rem; background: var(--ts-surface-container-low); border-radius: var(--ts-radius-md); border-left: 3px solid var(--ts-primary);">
                    <div class="ts-label-sm ts-text-secondary">In Progress</div>
                    <div class="ts-display-md ts-text-primary">@Model.InProgressTasks</div>
                </div>
                <div style="padding: 1rem; background: var(--ts-surface-container-low); border-radius: var(--ts-radius-md); border-left: 3px solid var(--ts-warning);">
                    <div class="ts-label-sm ts-text-secondary">Pending</div>
                    <div class="ts-display-md" style="color: var(--ts-warning);">@Model.PendingTasks</div>
                </div>
                <div style="padding: 1rem; background: var(--ts-surface-container-low); border-radius: var(--ts-radius-md); border-left: 3px solid var(--ts-secondary);">
                    <div class="ts-label-sm ts-text-secondary">Total</div>
                    <div class="ts-display-md ts-text-secondary">@Model.TotalTasks</div>
                </div>
            </div>
        }
    </div>
</div>

<!-- Per-Project Progress -->
@if (Model.GroupProgress.Any())
{
    <div class="ts-dashboard-card">
        <div class="ts-dashboard-card-header">
            <h3 class="ts-title-lg">
                <span class="material-symbols-outlined ts-text-primary" style="font-size: 20px;">folder_open</span>
                Progress by Project
            </h3>
        </div>
        <div class="ts-dashboard-card-body">
            @foreach (var proj in Model.GroupProgress.Values)
            {
                @if (proj.Total > 0)
                {
                    <div style="margin-bottom: 1.5rem;">
                        <div class="ts-flex ts-justify-between ts-mb-sm">
                            <span class="ts-label-md">@proj.GroupName</span>
                            <span class="ts-label-sm ts-text-secondary">@proj.Completed / @proj.Total</span>
                        </div>
                        <div style="height: 6px; background: var(--ts-surface-container-high); border-radius: 3px; overflow: hidden;">
                            <div style="height: 100%; width: @(proj.Percentage)%; background: var(--ts-success); border-radius: 3px; transition: width 0.3s ease;"></div>
                        </div>
                        <div class="ts-label-sm ts-text-secondary ts-mt-xs">@proj.Percentage.ToString("F0")% complete</div>
                    </div>
                }
            }
        </div>
    </div>
}
```

#### Step 4: Wire Up Model Binding
**File:** `TeamSync/Views/Home/StudentDashboard.cshtml` (top)

```razor
@model TeamSync.ViewModels.StudentDashboardViewModel
```

---

## Issue 2: Task Status Update Workflow Incomplete

### Current State
- Task model has status fields ✅
- Controller has action methods ⚠️ (may not be fully connected)
- Database structure ready ✅
- UI buttons partially wired

### Required Implementation

#### Step 1: Verify TasksController Methods
**File:** `TeamSync/Controllers/TasksController.cs`

**Methods to verify/implement:**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SetInProgress(int id)
{
    var task = await _context.Tasks.FindAsync(id);
    if (task == null) return NotFound();
    
    // Verify current user is assigned
    var currentUser = await _userManager.GetUserAsync(User);
    if (task.AssignedToId != currentUser?.Id) return Forbid();
    
    // Update status
    task.Status = "InProgress";
    task.UpdatedAt = DateTime.UtcNow;
    _context.Tasks.Update(task);
    await _context.SaveChangesAsync();
    
    TempData["SuccessMessage"] = "Task marked as in progress.";
    return RedirectToAction(nameof(Details), new { id = task.Id });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RequestReview(int id)
{
    var task = await _context.Tasks.FindAsync(id);
    if (task == null) return NotFound();
    
    // Verify current user is assigned
    var currentUser = await _userManager.GetUserAsync(User);
    if (task.AssignedToId != currentUser?.Id) return Forbid();
    
    // Update status
    task.Status = "ReviewRequested";
    task.ReviewRequestedById = currentUser.Id;
    task.ReviewRequestedAt = DateTime.UtcNow;
    task.UpdatedAt = DateTime.UtcNow;
    _context.Tasks.Update(task);
    await _context.SaveChangesAsync();
    
    TempData["SuccessMessage"] = "Task review requested.";
    return RedirectToAction(nameof(Details), new { id = task.Id });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ApproveCompletion(int id)
{
    var task = await _context.Tasks.FindAsync(id);
    if (task == null) return NotFound();
    
    var currentUser = await _userManager.GetUserAsync(User);
    if (currentUser == null) return Challenge();
    
    var group = await _context.Groups
        .Include(g => g.Members)
        .FirstOrDefaultAsync(g => g.Id == task.GroupId);
    
    if (group == null) return NotFound();
    
    var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
    bool isLead = currentMember?.Role == "Lead";
    bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
    bool isAdmin = User.IsInRole("Admin");
    
    // Only Lead or Professor can approve
    if (!isLead && !isProfessor && !isAdmin) return Forbid();
    
    if (task.Status == "ReviewRequested" && isLead && !isProfessor)
    {
        // Lead approval (first step)
        task.Status = "LeadApproved";
        task.LeadApprovedById = currentUser.Id;
        task.LeadApprovedAt = DateTime.UtcNow;
    }
    else if ((task.Status == "ReviewRequested" || task.Status == "LeadApproved") && (isProfessor || isAdmin))
    {
        // Professor/Admin final approval
        task.Status = "Completed";
        task.CompletionApprovedById = currentUser.Id;
        task.CompletionApprovedAt = DateTime.UtcNow;
    }
    else
    {
        return BadRequest("Invalid status transition.");
    }
    
    task.UpdatedAt = DateTime.UtcNow;
    _context.Tasks.Update(task);
    await _context.SaveChangesAsync();
    
    TempData["SuccessMessage"] = "Task approved successfully.";
    return RedirectToAction(nameof(Details), new { id = task.Id });
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> RejectCompletion(int id)
{
    var task = await _context.Tasks.FindAsync(id);
    if (task == null) return NotFound();
    
    var currentUser = await _userManager.GetUserAsync(User);
    if (currentUser == null) return Challenge();
    
    var group = await _context.Groups
        .Include(g => g.Members)
        .FirstOrDefaultAsync(g => g.Id == task.GroupId);
    
    if (group == null) return NotFound();
    
    var currentMember = group.Members.FirstOrDefault(m => m.UserId == currentUser.Id);
    bool isLead = currentMember?.Role == "Lead";
    bool isProfessor = await _userManager.IsInRoleAsync(currentUser, "Professor");
    bool isAdmin = User.IsInRole("Admin");
    
    if (!isLead && !isProfessor && !isAdmin) return Forbid();
    
    // Reset to InProgress for re-submission
    task.Status = "InProgress";
    task.LeadApprovedById = null;
    task.LeadApprovedAt = null;
    task.CompletionApprovedById = null;
    task.CompletionApprovedAt = null;
    task.UpdatedAt = DateTime.UtcNow;
    
    _context.Tasks.Update(task);
    await _context.SaveChangesAsync();
    
    TempData["SuccessMessage"] = "Task returned for rework.";
    return RedirectToAction(nameof(Details), new { id = task.Id });
}
```

#### Step 2: Verify UI Buttons in Groups/Details.cshtml
**File:** `TeamSync/Views/Groups/Details.cshtml` (already in your file)

The Active Tasks section already has the buttons. Verify they're working:
- ✅ "Start" button for Pending tasks
- ✅ "Request Review" button for InProgress tasks
- ✅ "Lead Approve" / "Finalize" buttons for ReviewRequested/LeadApproved
- ✅ "Reject" button for rework

#### Step 3: Add Status Display
**File:** `TeamSync/Views/Tasks/Details.cshtml`

Add status badge to task details:
```razor
<div class="ts-mb-md">
    <span class="ts-label-sm ts-text-secondary">Status</span>
    <div>
        <span class="ts-badge @TaskStatusBadge(Model.Status)">
            @Model.Status
        </span>
    </div>
</div>

@if (Model.CompletionApprovedAt.HasValue)
{
    <div class="ts-mb-md">
        <span class="ts-label-sm ts-text-secondary">Completed</span>
        <div class="ts-body-md">@Model.CompletionApprovedAt.Value.ToString("MMMM dd, yyyy HH:mm")</div>
    </div>
}
```

---

## Verification Checklist

### Before Merging
- [ ] All task status transitions work (Pending → InProgress → ReviewRequested → LeadApproved → Completed)
- [ ] Lead approval gates properly (only Leads can LeadApprove)
- [ ] Professor final approval works
- [ ] Reject functionality returns task to InProgress
- [ ] Completed tasks appear in CompletedTasks section
- [ ] Progress bar shows accurate percentages
- [ ] Progress bar updates on task completion
- [ ] Per-group progress calculated correctly
- [ ] No build errors or warnings
- [ ] All tests pass

### Testing Scenarios
1. **Happy Path**: Student → InProgress → ReviewRequested → LeadApproved → Completed
2. **Rejection Flow**: Start at ReviewRequested, reject, return to InProgress
3. **Multiple Groups**: Progress calculations correct with multiple groups
4. **Zero Tasks**: No errors when user has no tasks
5. **Permission Check**: Non-lead cannot lead-approve

---

## Timeline Estimate

| Task | Hours | Priority |
|------|-------|----------|
| Update StudentDashboardViewModel | 0.5 | High |
| Update HomeController with calculation logic | 1 | High |
| Update StudentDashboard view with progress bars | 2 | High |
| Verify TasksController methods | 1 | High |
| Fix any UI button wiring | 1 | High |
| Comprehensive testing | 2 | High |
| **Total** | **7.5 hours** | |

---

## Notes

- The data structure is already in place; this is mostly wiring work
- Completed tasks are now visible (just added)
- Focus on verification first before adding new features
- Consider adding email notifications when tasks are completed (future enhancement)

