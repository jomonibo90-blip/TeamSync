# Join Request Approval Implementation

## Overview
Implemented approval-required joins to match your workflow pattern. Students now need professor approval to join groups via join code, while professors can join directly.

## Changes Made

### 1. **New Model: JoinRequest**
- `TeamSync\Models\JoinRequest.cs`
- Tracks pending join requests from students
- Fields: GroupId, UserId, Status, ApprovedByUserId, CreatedAt, ResolvedAt

### 2. **Database Updates**
- Added `JoinRequests` DbSet to ApplicationDbContext
- Configured relationships with Group, User, ApprovedBy
- Created migration: `AddJoinRequestTable`

### 3. **ViewModel Updates**
- Added `PendingJoinRequests` list to `GroupDetailsViewModel`
- Created `JoinRequestViewModel` for UI display
- Shows: UserFullName, Email, Status, CreatedAt

### 4. **Controller Logic (GroupsController)**

#### Join Action (Modified)
- **Professors**: Join directly (no approval)
- **Students**: Submit join request for professor approval
- Prevents duplicate pending requests

#### New Approval Actions
- `ApproveJoinRequest(joinRequestId)` - Professor approves, member is added
- `RejectJoinRequest(joinRequestId)` - Professor rejects request

#### Details Action (Modified)
- Loads pending `JoinRequests` from database
- Maps to `JoinRequestViewModel` for display

### 5. **UI Updates (Details.cshtml)**
- New card: "Pending Join Requests" (displays for professors/admins)
- Shows user name, email, and creation date
- Action buttons: Approve, Reject
- Styled with green accent (success color)

## Workflow

```
Student uses join code    → JoinRequest (Pending)        → Professor approves/rejects
Professor uses join code  → Direct join (no approval)
```

## Database Schema

```sql
CREATE TABLE JoinRequests (
    Id INT PRIMARY KEY IDENTITY,
    GroupId INT NOT NULL FOREIGN KEY (Groups),
    UserId NVARCHAR NOT NULL FOREIGN KEY (Users),
    Status NVARCHAR NOT NULL, -- "Pending", "Approved", "Rejected"
    ApprovedByUserId NVARCHAR FOREIGN KEY (Users),
    CreatedAt DATETIME2 NOT NULL,
    ResolvedAt DATETIME2
)
```

## Complete Group Membership Workflow

Now you have a **fully approved workflow** for group membership:

```
JOINING A GROUP:
- Student uses join code        → Join Request (pending) → Professor approves → Member
- Professor uses join code      → Direct join (no approval)

ADDING A MEMBER:
- Lead adds by email            → Add Member Request (pending) → Professor approves → Member
- Professor adds by email       → Direct add (no approval)

REMOVING A MEMBER:
- Lead removes member           → Removal Request (pending) → Professor approves → Removed
- Student leaves group          → Leave Request (pending) → Professor approves → Removed
- Professor removes member      → Direct removal (no approval)
- Professor leaves group        → Direct departure (no approval)
```

## Features

✅ **Approval-required joins** - Students need professor approval  
✅ **Direct professor joins** - Professors bypass approval  
✅ **Duplicate prevention** - Can't create multiple pending requests for same user  
✅ **Validation** - Checks if user exists, not already member, etc.  
✅ **UI display** - Shows pending requests with actions  
✅ **Consistent pattern** - Matches removal/addition workflows  
✅ **Build successful** - No errors or warnings

## Migration Command

```bash
dotnet ef migrations add AddJoinRequestTable
dotnet ef database update
```

## User Experience

**For Students:**
1. Enter join code
2. Submit request
3. Message: "Join request sent to professor for approval"
4. Wait for professor to approve/reject
5. Upon approval: Added as member

**For Professors:**
1. See "Pending Join Requests" on group Details page
2. Click "Approve" to add student
3. Click "Reject" to deny request
4. Can join any group directly with code
