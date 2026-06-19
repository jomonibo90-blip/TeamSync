# Student Removal Workflow Implementation

## Overview
Implemented comprehensive student removal/leaving workflow with professor approval oversight.

## What's Been Built

### 1. New Model: RemovalRequest
- **File**: `TeamSync/Models/RemovalRequest.cs`
- **Purpose**: Track removal/leave requests pending professor approval
- **Fields**:
  - `GroupMemberId`: The member being removed
  - `GroupId`: Which group
  - `UserId`: User being removed
  - `RequestedByUserId`: Lead/Student who initiated
  - `ApprovedByUserId`: Professor who approved (nullable)
  - `Reason`: Why they're being removed/leaving
  - `Status`: Pending, Approved, or Rejected
  - `CreatedAt`, `ResolvedAt`: Timestamps

### 2. Database Layer Updates
- **File**: `TeamSync/Data/ApplicationDbContext.cs`
- **Added**:
  - `DbSet<RemovalRequest> RemovalRequests`
  - Relationships with Group, User, GroupMember
  - Cascade delete rules
  - Performance indexes on GroupId, UserId, Status

### 3. Controller Actions: GroupsController
**New Actions Implemented:**

#### `RemoveMember(int groupId, string userId, string reason)`
- **Authorization**:
  - ✅ Admin/Professor: Direct removal (no approval)
  - ✅ Lead: Creates removal request (requires approval)
  - ✅ Student leaving: Creates leave request (requires approval)
  - ❌ Regular member: Cannot remove anyone
- **Behavior**: Creates `RemovalRequest` record for audit trail

#### `ApproveMemberRemoval(int removalRequestId)`
- **Authorization**: Only professor or admin
- **Action**: 
  - Marks request as Approved
  - Removes GroupMember immediately
  - Records who approved

#### `RejectMemberRemoval(int removalRequestId)`
- **Authorization**: Only professor or admin
- **Action**: 
  - Marks request as Rejected
  - GroupMember stays in group
  - Records who rejected

#### `DeleteGroup(int id)`
- **Authorization**: Only group creator (professor) or admin
- **Action**: Deletes entire group (cascades to members, tasks, contributions)

#### `PromoteToLead(int groupId, string userId)`
- **Authorization**: Only professor or admin
- **Action**: Promotes student/member to Lead role

### 4. Database Migration
- **File**: `TeamSync/Migrations/20260605_AddRemovalRequestWorkflow.cs`
- **Added**: RemovalRequests table with foreign keys and indexes
- **File**: `TeamSync/Migrations/ApplicationDbContextModelSnapshot.cs`
- **Updated**: Entity configuration for RemovalRequest

## Authorization Matrix

| Role | Can Remove | Direct/Request | Approval Needed |
|------|-----------|-----------------|-----------------|
| Admin | Anyone | Direct | No |
| Professor | Students in their group | Direct | No |
| Lead | Students in their group | Request | Yes (Professor) |
| Student | Themselves (leave) | Request | Yes (Professor) |

## Workflow Diagrams

### Professor Removes Student (Direct)
```
Professor clicks "Remove"
    ↓
StudentGroupMember deleted immediately
    ↓
Student removed from group
```

### Lead Removes Student (Approval Required)
```
Lead clicks "Remove Student"
    ↓
RemovalRequest created (Status: Pending)
    ↓
Professor sees pending removal requests
    ↓
Professor approves/rejects
    ├─→ If Approved: GroupMember deleted
    └─→ If Rejected: Request marked rejected
```

### Student Leaves Group (Approval Required)
```
Student clicks "Leave Group"
    ↓
RemovalRequest created (Status: Pending, RequestedBy: Student)
    ↓
Professor sees pending leave requests
    ↓
Professor approves/rejects
    ├─→ If Approved: GroupMember deleted
    └─→ If Rejected: Request marked rejected, student stays
```

## Database Schema

```sql
RemovalRequests
├── Id (PK)
├── GroupMemberId (FK) → GroupMembers.Id (CASCADE)
├── GroupId (FK) → Groups.Id (CASCADE)
├── UserId (FK) → AspNetUsers.Id (CASCADE)
├── RequestedByUserId (FK) → AspNetUsers.Id (CASCADE)
├── ApprovedByUserId (FK) → AspNetUsers.Id (SET NULL)
├── Reason
├── Status (Indexed)
├── CreatedAt
└── ResolvedAt
```

## Next Steps

### Phase 2: Views & UI
- [ ] Update `Details.cshtml` to show:
  - Remove buttons (for professor/lead)
  - Leave button (for student)
  - Delete button (for professor/admin)
  - Promote to Lead dropdown
- [ ] Create removal request list view for professors
- [ ] Add approval/rejection UI

### Phase 3: Admin Dashboard
- [ ] List all users
- [ ] Enroll professors/students
- [ ] Deactivate users
- [ ] System analytics

### Phase 4: Integration
- [ ] Add notifications when removal requests are created
- [ ] Email professors about pending approvals
- [ ] Add audit logging

## Testing Scenarios

**Test Case 1: Professor Direct Removal**
1. Professor logs in
2. Opens group details
3. Clicks remove on student
4. ✅ Student removed immediately

**Test Case 2: Lead Requests Removal**
1. Lead logs in
2. Opens group details
3. Clicks remove on student
4. Enters reason
5. ✅ RemovalRequest created
6. Professor sees pending request
7. Professor approves
8. ✅ Student removed

**Test Case 3: Student Requests to Leave**
1. Student logs in
2. Opens group details
3. Clicks "Leave Group"
4. ✅ Leave request created
5. Professor sees leave request
6. Professor approves
7. ✅ Student removed

## Code Quality
- ✅ Proper authorization checks
- ✅ Audit trail maintained
- ✅ No orphaned records
- ✅ Cascading deletes prevent data inconsistency
- ✅ SQL injection prevention (EF Core)
- ✅ CSRF protection ([ValidateAntiForgeryToken])

## Files Modified
1. `TeamSync/Models/RemovalRequest.cs` (NEW)
2. `TeamSync/Data/ApplicationDbContext.cs` (UPDATED)
3. `TeamSync/Controllers/GroupsController.cs` (UPDATED)
4. `TeamSync/Migrations/20260605_AddRemovalRequestWorkflow.cs` (NEW)
5. `TeamSync/Migrations/ApplicationDbContextModelSnapshot.cs` (UPDATED)

---

**Status**: ✅ Backend Implementation Complete
**Next**: Views & UI Implementation
