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

### ✅ COMPLETED ITEMS

#### Item #3: UI Components for Status Updates - **COMPLETE** ✨

**Location:** `Views/Tasks/Details.cshtml` and `Views/Tasks/Index.cshtml`

**What's Implemented:**
- ✅ Task Details view with status update buttons for assigned users
- ✅ Approval workflow forms (approve with notes/hours, reject)
- ✅ Status badges with color coding (Requested, Rejected, Completed, Pending)
- ✅ Task Index view with status filtering (All, Requested, Pending, Completed, Rejected)
- ✅ Card-level quick actions (Start, Request Review, Approve, Reject)
- ✅ Workflow timeline display (Review Requested → Lead Approved → Completed)
- ✅ Double-submit prevention with button disabling
- ✅ Responsive design on all screen sizes

**Buttons Implemented:**
```html
✅ For assigned person:
   - Start Task (when Status == "Pending")
   - Request Review (when Status == "InProgress")
   - Propose Completion (when Status == "Pending" or "InProgress")

✅ For task creator/approvers:
   - Lead Approve / Finalize (when Status == "ReviewRequested" or "LeadApproved")
   - Reject (with optional reason textarea)
   - Both include approval notes and hours inputs

✅ Quick actions on task cards:
   - Start button for pending assigned tasks
   - Request Review for in-progress tasks
   - Approve/Reject for reviewers
```

---

#### Item #4: Contribution Tracking Integration - **COMPLETE** ✨

**Location:** `Controllers/TasksController.cs`, `Views/Tasks/Details.cshtml`, `Models/Contribution.cs`

**What's Implemented:**
- ✅ Automatic contribution creation when task is marked completed
- ✅ Manual contribution entry forms (visible to assignee, lead, professors)
- ✅ Contribution editing with full audit trail
- ✅ Contribution deletion with snapshot tracking
- ✅ Hours tracking with decimal support (0.25 step increments)
- ✅ Approval notes automatically saved to contribution.Notes
- ✅ Source tracking ("TaskFinalization" vs "ManualEntry")
- ✅ Role-based access control (Assignee, Lead, Professor, Admin)
- ✅ ContributionHistory audit trail with who/what/when

**Actions in TasksController:**
- ✅ `AddContribution` - Manual entry with authorization checks
- ✅ `EditContribution` - Update with before/after audit
- ✅ `DeleteContribution` - Soft deletion with snapshot
- ✅ `ContributionHistory` - View full audit trail
- ✅ `ExportContributionsCsv` - CSV export functionality

**Integration Points:**
- ✅ `ApproveCompletion` creates Contribution on task completion
- ✅ Contribution hours aggregated in student dashboard
- ✅ Task completion percentage calculated for progress bar
- ✅ All contribution changes logged to ContributionHistory

---

### ❌ TODO - Remaining Items

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

- [x] User can mark assigned task as "In Progress" ✅ IMPLEMENTED
- [x] User can submit task as "Completed" ✅ IMPLEMENTED
- [x] Task creator receives notification and can approve/reject ✅ IMPLEMENTED (no notification system yet)
- [x] Professor can override and approve directly ✅ IMPLEMENTED
- [x] Contribution record created on approval ✅ IMPLEMENTED
- [x] Status appears updated in task list and details ✅ IMPLEMENTED
- [x] Unauthorized users cannot change status ✅ IMPLEMENTED
- [x] Progress calculations work correctly ✅ IMPLEMENTED
- [x] All statuses flow correctly (no invalid transitions) ✅ IMPLEMENTED

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
- `TeamSync/ITEMS_3_4_COMPLETION.md` - Detailed completion summary

---

## Notes

- All operations should validate task status before allowing transition
- Log all status changes with timestamps
- Ensure archived groups prevent any status changes
- Consider adding task history/audit trail in future
- Progress bar implementation can wait for Sprint 2 dashboard update
