# TeamSync - Sprint 1 Implementation Complete

## Project Overview
TeamSync is a centralized academic collaboration platform designed to help students and professors manage group projects effectively.

## Sprint 1 Completed Tasks

### ✅ 1. Project Setup and GitHub Repository Configuration
- **Status**: Completed
- **Details**:
  - ASP.NET Core MVC project with .NET 10 targeting
  - NuGet packages configured for Entity Framework Core, Identity, and SQL Server
  - Project file updated with all required dependencies
  - `.gitignore` configured to exclude sensitive files and build artifacts

### ✅ 2. Database Schema Planning and Entity Framework Setup

#### Database Models Created:
1. **User Model** (`Models/User.cs`)
   - Extends `IdentityUser` for authentication
   - Properties: FirstName, LastName, StudentId, CreatedAt, UpdatedAt, IsActive
   - Navigation properties for groups, tasks, and contributions

2. **Group Model** (`Models/Group.cs`)
   - Properties: Name, Description, CreatedById, CreatedAt, UpdatedAt, IsActive
   - One-to-many relationships with GroupMembers and Tasks

3. **GroupMember Model** (`Models/GroupMember.cs`)
   - Properties: GroupId, UserId, Role (Member/Lead/Instructor), JoinedAt, IsActive
   - Junction table for Group-User relationships

4. **Task Model** (`Models/Task.cs`)
   - Properties: Title, Description, AssignedToId, CreatedById, DueDate, Status, Priority
   - Support for task tracking and contribution monitoring

5. **Contribution Model** (`Models/Contribution.cs`)
   - Properties: TaskId, UserId, Description, ContributedAt, HoursSpent
   - Tracks individual student contributions to tasks

#### Entity Framework Configuration (`Data/ApplicationDbContext.cs`)
- DbContext configured with Identity support
- Relationships defined with proper foreign keys
- Cascade delete behaviors configured appropriately
- Database indexes created for common query patterns
- Connection string: LocalDB (for development)

### ✅ 3. User Registration and Authentication System

#### Authentication Features Implemented:
1. **Account Controller** (`Controllers/AccountController.cs`)
   - User registration with validation
   - Secure login with lockout protection
   - Logout functionality
   - Two-factor authentication support
   - Proper error handling and logging

2. **View Models** (`ViewModels/AccountViewModels.cs`)
   - `RegisterViewModel`: For user registration
   - `LoginViewModel`: For user login
   - `LoginWith2faViewModel`: For two-factor authentication
   - All with proper data annotations and validation

3. **Razor Views** (`Views/Account/`)
   - `Register.cshtml`: Bootstrap-based registration form
   - `Login.cshtml`: Bootstrap-based login form
   - `LoginWith2fa.cshtml`: Two-factor authentication form
   - `Lockout.cshtml`: Account lockout notification
   - All views use Bootstrap CSS framework for responsive design

#### Identity Configuration (`Program.cs`)
- Password policy: 8+ characters, uppercase, lowercase, digits
- Email uniqueness requirement
- Account lockout after failed attempts
- Token providers configured
- Authentication and authorization middleware added

### ✅ 4. Database Initialization Service

#### DbInitializerService (`Services/DbInitializerService.cs`)
- Automatic database migration on application startup
- Seed default roles: Admin, Student, Professor
- Sample user creation:
  - Admin account: `admin@teamsync.com` (Password: Admin@123456)
  - Professor account: `professor@teamsync.com` (Password: Professor@123456)
  - Student accounts: 3 sample students with password Student@123456

## Technology Stack Implemented
- **Backend**: ASP.NET Core MVC with C#
- **Database**: Microsoft SQL Server (LocalDB for development)
- **ORM**: Entity Framework Core 8.0
- **Authentication**: ASP.NET Core Identity
- **Frontend**: Razor Views, Bootstrap 5
- **Framework**: .NET 10

## Connection String
```
Server=(localdb)\mssqllocaldb;Database=TeamSync;Trusted_Connection=true;TrustServerCertificate=true;
```

## Getting Started

### Prerequisites
- Visual Studio 2022 or later
- .NET 10 SDK
- SQL Server LocalDB (included with Visual Studio)

### Setup Instructions

1. **Clone the Repository**
   ```bash
   git clone https://github.com/jomonibo90-blip/TeamSync.git
   cd TeamSync
   ```

2. **Open in Visual Studio**
   ```bash
   start TeamSync.sln
   ```

3. **Build the Project**
   - Use Visual Studio menu: Build → Build Solution
   - Or use CLI: `dotnet build`

4. **Run the Application**
   - Press F5 in Visual Studio or use: `dotnet run`
   - Application will open at `https://localhost:5001`
   - Database will be created and initialized automatically

5. **Test Login**
   - Navigate to Login page
   - Use credentials:
     - Email: `student1@teamsync.com`
     - Password: `Student@123456`

## Key Architectural Decisions

1. **Identity Integration**: Used ASP.NET Core Identity for secure user management with built-in features like lockout and two-factor authentication.

2. **Database Design**: Relational schema with proper foreign keys and indexes for performance.

3. **Separation of Concerns**: Controllers, Views, ViewModels, and Services clearly separated.

4. **Automatic Migrations**: DbInitializerService ensures database schema is always up-to-date on startup.

5. **Bootstrap UI**: Responsive design framework for consistent user experience across devices.

## Sprint 1 Deliverables Summary
✅ Project setup with all dependencies
✅ Complete database schema design
✅ Entity Framework Core configuration
✅ ASP.NET Identity implementation
✅ User registration system
✅ Secure login system
✅ Account lockout protection
✅ Two-factor authentication support
✅ Responsive UI views
✅ Database initialization service
✅ Sample data seeding
✅ Comprehensive logging

## Next Steps (Sprint 2)
- Role-based access control (RBAC)
- Group creation and management features
- Task assignment functionality
- Task tracking and status updates
- Initial dashboard development

## Notes for Team
- All passwords are temporary and should be changed on first login
- The application uses LocalDB for local development; update connection string for production
- Ensure all team members have SQL Server LocalDB installed
- Review Entity Framework migrations before pushing to repository
