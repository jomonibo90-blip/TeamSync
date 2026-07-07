# Task Tracking & Status Updates - Next Collaborator TODO

This document outlines the remaining work for completing the task management system. The **task assignment functionality** has been completed and is ready. The following work items are for the next collaborator focused on **task tracking and status updates**.

---

## ✅ Completed (By Previous Collaborator)

### Task Assignment System
- ✅ Task model created with all necessary properties
- ✅ TasksController assignment/request workflow implemented (create, edit, view, request, approve/reject request)
- ✅ Task creation by Professors/Leads for groups
- ✅ Task editing (title, description, assignee, due date, priority)
- ✅ Task viewing with role-based access control
- ✅ Student task request workflow (RequestTask action)
- ✅ Task request approval/rejection by Prof/Lead/Admin
- ✅ Request approval scheduling: approver sets due date and can override assignee (defaults to requester)
- ✅ ViewModels: TaskListItemViewModel, TaskCreateViewModel, TaskRequestViewModel, TaskEditViewModel
- ✅ Views: Tasks/Index.cshtml, Tasks/Details.cshtml, Tasks/Edit.cshtml, Tasks/Request.cshtml
- ✅ UI Button for students to request tasks in Group Details view
- ✅ Authorization checks on all endpoints
- ✅ Database migrations for Task model
- ✅ Comprehensive logging and error handling

---

## ❌ TODO - Task Tracking Phase (NEXT PHASE)

### 0. Task Deletion / Archival Governance (Lead/Professor)

**Scope owner:** Task tracking/status collaborator

Add lifecycle-safe task removal rules (not assignment scope):

- Implement `Delete`/`Archive` action(s) for tasks
- Only allow **Lead/Professor/Admin** (and optionally creator based on policy)
- Block deletion for locked/finalized states, or convert to soft-delete/archive
- Record audit metadata (who deleted/archived, when, reason)
- Ensure deleted/archived tasks are excluded from active lists but still available for accountability reporting

Suggested policy:
- Prefer **soft archive** over hard delete for accountability history
- Hard delete only for invalid/duplicate requests and admin-approved cleanup

---

### 1. Task Status Update Functionality

**Location:** `TasksController.cs`

Add new actions to allow task status updates:

```csharp
// POST action to mark task as "In Progress"
[HttpPost]
public async Task<IActionResult> MarkInProgress(int taskId)
{
    // Assigned person or professor/lead can mark as In Progress
    // Status validation: Only update if currently "Pending"
    // Authorization: AssignedToId == currentUser OR Professor/Lead/Admin
    // Update UpdatedAt timestamp
}

// POST action to mark task as "Completed"
[HttpPost]
public async Task<IActionResult> MarkCompleted(int taskId)
{
    // Assigned person submits completion
    // Status: "Pending" -> "Completed"
    // Requires approval from task creator (CreatedById)
    // Add Contribution record for tracking
}

// POST action to mark task as "Ready for Review"
[HttpPost]
public async Task<IActionResult> MarkReadyForReview(int taskId)
{
    // Similar to MarkCompleted but status = "Ready for Review"
    // Requires task creator approval
}
```

**Authorization Rules:**
- **Assigned person** can mark task as "In Progress" or "Completed"
- **Task creator** must approve final status changes (Completed/Ready for Review)
- **Professor/Admin** can override and approve directly
- **Lead** can approve tasks within their group

---

### 2. Task Approval Workflow for Completion

**Models to Update:** `Task.cs`

Add these properties to track approval:
```csharp
public string? ApprovedById { get; set; }  // Who approved the completion
public DateTime? ApprovedAt { get; set; }   // When it was approved
public string? ApprovalNotes { get; set; }  // Why approved/rejected
```

**Actions:**
```csharp
[HttpPost]
public async Task<IActionResult> ApproveCompletion(int taskId, string notes)
{
    // Only CreatedById or Professor/Admin can approve
    // Changes status from "Completed" to "Completed" (with ApprovedById set)
    // Creates Contribution record for accountability
    // Send notification to assigned person
}

[HttpPost]
public async Task<IActionResult> RejectCompletion(int taskId, string reason)
{
    // Only CreatedById or Professor/Admin can reject
    // Changes status back to "In Progress"
    // Includes reason for rejection
}
```

---

### 3. UI Components for Status Updates

**Update Files:**
- `Views/Tasks/Details.cshtml` - Add buttons for status transitions
- `Views/Tasks/Index.cshtml` - Show status badge with filtering

**Buttons to Add:**
```html
<!-- For assigned person -->
@if (Model.AssignedToId == currentUserId && Model.Status == "Pending")
{
    <button>Mark In Progress</button>
    <button>Mark Completed</button>
    <button>Mark Ready for Review</button>
}

<!-- For task creator (approval) -->
@if (Model.CreatedById == currentUserId && (Model.Status == "Completed" || Model.Status == "Ready for Review"))
{
    <button>Approve</button>
    <button>Request Changes</button>
}

<!-- For Professor/Admin (override) -->
@if (isProfessor || isAdmin)
{
    <!-- Show all status change buttons -->
}
```

---

### 4. Contribution Tracking Integration

**Create action when task is approved:**
```csharp
var contribution = new Contribution
{
    UserId = task.AssignedToId,
    TaskId = task.Id,
    GroupId = task.GroupId,
    ContributionType = "TaskCompletion",
    Description = $"Completed task: {task.Title}",
    HoursSpent = 0,  // Optional: let user specify
    CreatedAt = DateTime.UtcNow
};
_context.Contributions.Add(contribution);
```

---

### 5. Progress Dashboard Integration

**Data needed for Progress Bar:**
- Count of tasks by status in each group
- Calculate: `(Completed Tasks / Total Tasks) * 100 = Progress %`
- Filter by group and sprint/date range

**Suggested View Updates:**
- `Views/Home/StudentDashboard.cshtml` - Show group progress
- `Views/Home/ProfessorDashboard.cshtml` - Show student task completion rates

---

### 6. Database Migration

**Create migration:** `AddTaskApprovalFields`
```
Add columns: ApprovedById, ApprovedAt, ApprovalNotes to Tasks table
```

---

## Testing Checklist

- [ ] User can mark assigned task as "In Progress"
- [ ] User can submit task as "Completed"
- [ ] Task creator receives notification and can approve/reject
- [ ] Professor can override and approve directly
- [ ] Contribution record created on approval
- [ ] Status appears updated in task list and details
- [ ] Unauthorized users cannot change status
- [ ] Progress calculations work correctly
- [ ] All statuses flow correctly (no invalid transitions)

---

## Authorization Reference

From `copilot-instructions.md`:
> "Final status changes must be approved by the task creator (`CreatedById`) with oversight from a professor."

This means:
- Task creator can mark as complete (proposes)
- Professor must approve (oversees)
- Alternative: Student marks complete → Auto-notifies creator + professor → They approve

---

## Related Files

**Key Files to Review:**
- `TeamSync/Controllers/TasksController.cs` - Main controller
- `TeamSync/Models/Task.cs` - Task model
- `TeamSync/Models/Contribution.cs` - For integration
- `TeamSync/Views/Tasks/Details.cshtml` - UI for buttons
- `TeamSync/Data/ApplicationDbContext.cs` - DB context

---

## Notes

- All operations should validate task status before allowing transition
- Log all status changes with timestamps
- Ensure archived groups prevent any status changes
- Consider adding task history/audit trail in future
- Progress bar implementation can wait for Sprint 2 dashboard update
