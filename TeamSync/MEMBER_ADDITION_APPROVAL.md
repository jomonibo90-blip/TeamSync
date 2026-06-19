# Member Addition Approval Workflow Implementation

## Overview
Implemented approval-required member additions to match the removal workflow pattern. This ensures professors maintain oversight and control over group composition.

## Changes Made

### 1. **New Model: AddMemberRequest**
- `TeamSync\Models\AddMemberRequest.cs`
- Tracks pending member addition requests
- Fields: GroupId, UserId, Email, RequestedByUserId, Status, ApprovedByUserId, CreatedAt, ResolvedAt

### 2. **Database Updates**
- Added `AddMemberRequests` DbSet to ApplicationDbContext
- Configured relationships with Group, User, RequestedBy, ApprovedBy
- Created migration: `AddAddMemberRequestTable`

### 3. **ViewModel Updates**
- Added `PendingAddRequests` list to `GroupDetailsViewModel`
- Created `AddMemberRequestViewModel` for UI display

### 4. **Controller Logic (GroupsController)**

#### AddMember Action
- **Professors/Admins**: Add members directly (no approval needed)
- **Leads**: Submit add request for professor approval
- Checks for existing pending requests to prevent duplicates

#### New Approval Actions
- `ApproveAddMember(addMemberRequestId)` - Professor approves, member is added
- `RejectAddMember(addMemberRequestId)` - Professor rejects request

#### Details Action
- Loads pending `AddMemberRequests` from database
- Maps to `AddMemberRequestViewModel` for display

### 5. **UI Updates (Details.cshtml)**
- New card: "Pending Member Add Requests" (displays for professors/admins)
- Shows email, requested by, and creation date
- Action buttons: Approve, Reject
- Styled with blue accent (info color)

## Workflow

```
Lead adds member         → AddMemberRequest (Pending)     → Professor approves/rejects
Professor adds member    → Direct addition (no approval)
Admin adds member        → Direct addition (no approval)
```

## Database Schema

```sql
CREATE TABLE AddMemberRequests (
    Id INT PRIMARY KEY IDENTITY,
    GroupId INT NOT NULL FOREIGN KEY (Groups),
    UserId NVARCHAR FOREIGN KEY (Users),
    Email NVARCHAR NOT NULL,
    RequestedByUserId NVARCHAR NOT NULL FOREIGN KEY (Users),
    Status NVARCHAR NOT NULL, -- "Pending", "Approved", "Rejected"
    ApprovedByUserId NVARCHAR FOREIGN KEY (Users),
    CreatedAt DATETIME2 NOT NULL,
    ResolvedAt DATETIME2
)
```

## Features

✅ **Approval-required additions** - Leads must get professor approval  
✅ **Direct additions** - Professors/Admins bypass approval  
✅ **Duplicate prevention** - Can't create multiple pending requests for same user  
✅ **Validation** - Checks if user exists, not already member, etc.  
✅ **UI display** - Shows pending requests with requester and actions  
✅ **Consistent pattern** - Mirrors removal workflow design  

## Migration Command

```bash
dotnet ef migrations add AddAddMemberRequestTable
dotnet ef database update
```

## Testing Scenarios

1. **Lead adds member**
   - Add member → Appear in "Pending Member Add Requests"
   - Professor approves → Member added, request resolved
   - Professor rejects → Request marked rejected

2. **Professor adds member**
   - Add member → Member added directly
   - No pending request created

3. **Admin adds member**
   - Add member → Member added directly
   - No pending request created

4. **Edge cases**
   - User doesn't exist → Reject and show error
   - User already member → Reject and show error
   - Duplicate pending request → Error message
