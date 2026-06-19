# TeamSync Development Setup Guide
**Team accountability, simplified**

## Prerequisites

### System Requirements
- **OS**: Windows 10 or later, macOS 10.15+, or Linux
- **RAM**: 4GB minimum (8GB recommended)
- **.NET 10 SDK**: Download from https://dotnet.microsoft.com/en-us/download
- **Visual Studio 2022**: Community Edition or higher (https://visualstudio.microsoft.com/)
- **SQL Server LocalDB**: Usually included with Visual Studio installation
- **Git**: For version control (https://git-scm.com/)

### Visual Studio 2022 Installation
When installing Visual Studio, ensure you select:
- ✅ ASP.NET and web development
- ✅ .NET desktop development
- ✅ Data storage and processing

## Initial Setup Steps

### 1. Clone the Repository
```bash
git clone https://github.com/jomonibo90-blip/TeamSync.git
cd TeamSync
```

### 2. Verify .NET 10 Installation
```bash
dotnet --version
```
Should output version 10.0.x or higher.

### 3. Restore NuGet Packages
```bash
dotnet restore
```

Or in Visual Studio: Build → Clean Solution → Build Solution

### 4. Open in Visual Studio
```bash
start TeamSync.sln
```

### 5. Build the Solution
- Visual Studio: Press `Ctrl+Shift+B` or Build → Build Solution
- CLI: `dotnet build`

### 6. Run the Application
- **Visual Studio**: Press `F5` or Debug → Start Debugging
- **CLI**: `dotnet run` (ensure you're in TeamSync directory)

The application will start at `https://localhost:5001` (or similar).

## First Run Experience

### Database Creation
On first run, the following occurs automatically:
1. Entity Framework creates the LocalDB database named `TeamSync`
2. All migrations are applied
3. Sample users and roles are seeded

### Initial Test Accounts

| Role | Email | Password | Notes |
|------|-------|----------|-------|
| Admin | admin@teamsync.com | Admin@123456 | System administrator |
| Professor | professor@teamsync.com | Professor@123456 | Course instructor |
| Student | student1@teamsync.com | Student@123456 | Sample student 1 |
| Student | student2@teamsync.com | Student@123456 | Sample student 2 |
| Student | student3@teamsync.com | Student@123456 | Sample student 3 |

⚠️ **Security Note**: These are development accounts only. Change passwords for production!

## Project Structure

```
TeamSync/
├── Controllers/              # MVC Controllers
│   └── AccountController.cs # Authentication logic
├── Models/                   # Data models
│   ├── User.cs
│   ├── Group.cs
│   ├── GroupMember.cs
│   ├── Task.cs
│   └── Contribution.cs
├── Views/                    # Razor Views
│   ├── Account/
│   │   ├── Register.cshtml
│   │   ├── Login.cshtml
│   │   ├── LoginWith2fa.cshtml
│   │   └── Lockout.cshtml
│   └── Shared/               # Layout and shared views
├── ViewModels/               # View Models
│   └── AccountViewModels.cs
├── Data/                     # Database context
│   └── ApplicationDbContext.cs
├── Services/                 # Business logic services
│   └── DbInitializerService.cs
├── Migrations/               # EF Core migrations (auto-generated)
├── wwwroot/                  # Static files (CSS, JS, images)
├── appsettings.json          # Configuration settings
├── Program.cs                # Application startup
└── TeamSync.csproj          # Project file
```

## Common Development Tasks

### Adding a New Feature

1. **Create Models** (if needed)
   ```csharp
   // Models/YourModel.cs
   public class YourModel
   {
       public int Id { get; set; }
       public string Name { get; set; }
   }
   ```

2. **Add DbSet to ApplicationDbContext**
   ```csharp
   public DbSet<YourModel> YourModels { get; set; }
   ```

3. **Create Migration**
   ```bash
   dotnet ef migrations add AddYourModel
   ```

4. **Create Controller**
   ```bash
   # Add new controller file in Controllers/
   ```

5. **Create Views**
   ```bash
   # Add new views in Views/YourController/
   ```

6. **Update appsettings.json** (if needed for new config)

### Running Tests
```bash
dotnet test
```
(Note: Test projects can be added in Sprint 2+)

### Checking Dependencies
```bash
dotnet list package --outdated
```

### Updating Packages
```bash
dotnet package update
```

## Debugging

### Enable Debug Logging
Add to `appsettings.json`:
```json
"Logging": {
  "LogLevel": {
    "Default": "Debug",
    "Microsoft": "Debug",
    "Microsoft.AspNetCore": "Debug"
  }
}
```

### Visual Studio Debugging
1. Set breakpoints (click left margin of code line)
2. Press `F5` to start debugging
3. Use Debug → Windows → Locals to inspect variables
4. Press `Shift+F5` to stop debugging

### Output Window
View → Output (Ctrl+Alt+O) shows console output and logs.

## Performance Tips

1. **Use Async Methods**: Always use `async/await` for database operations
2. **Eager Loading**: Use `.Include()` to load related data efficiently
3. **Select Only Needed Fields**: Avoid loading entire entities when you only need specific columns
4. **Add Indexes**: Key foreign keys and frequently queried columns are indexed

## Common Issues and Solutions

### Issue: "Connection string 'DefaultConnection' not found"
**Solution**: Ensure `appsettings.json` exists and contains the ConnectionStrings section.

### Issue: "Database already exists" error
**Solution**: 
```bash
dotnet ef database drop
dotnet ef database update
```

### Issue: HTTPS certificate errors
**Solution**: Run the following commands:
```bash
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### Issue: Port 5001 already in use
**Solution**: Change the port in `launchSettings.json`:
```json
"applicationUrl": "https://localhost:5002;http://localhost:3000"
```

### Issue: NuGet package restore fails
**Solution**: 
```bash
dotnet nuget locals all --clear
dotnet restore
```

## Source Control Workflow

### Daily Workflow
```bash
# Start of day: Get latest changes
git pull origin main

# Work on your feature
# ... make changes ...

# End of day: Commit and push
git add .
git commit -m "Descriptive message of changes"
git push origin main
```

### Creating a Feature Branch
```bash
git checkout -b feature/your-feature-name
# ... make changes ...
git add .
git commit -m "Add your feature"
git push origin feature/your-feature-name
# Create Pull Request on GitHub
```

### Files to NOT Commit
The `.gitignore` file already excludes:
- `/bin/` and `/obj/` (build output)
- `.vs/` (Visual Studio cache)
- `*.db` and `*.db-*` (local database files)
- `.env` files (secrets)

## Documentation

- **Project Proposal**: See root directory project proposal document
- **Sprint 1 Completion**: See `SPRINT1_COMPLETION.md`
- **Migrations Guide**: See `MIGRATIONS_GUIDE.md`
- **This File**: Development Setup Guide

## Team Communication

- **WhatsApp**: Day-to-day discussions and updates
- **GitHub Issues**: Feature requests and bug tracking
- **GitHub Discussions**: Technical discussions
- **Weekly Meetings**: Every [Day] at [Time]

## Additional Resources

- **Entity Framework Core**: https://docs.microsoft.com/en-us/ef/core/
- **ASP.NET Core MVC**: https://docs.microsoft.com/en-us/aspnet/core/mvc/overview
- **Bootstrap Documentation**: https://getbootstrap.com/docs/
- **.NET Best Practices**: https://docs.microsoft.com/en-us/dotnet/standard/design-guidelines/
- **Git Basics**: https://git-scm.com/book/en/v2

## Contact & Support

For questions or issues:
1. Check existing GitHub Issues
2. Post in GitHub Discussions
3. Contact team members via WhatsApp
4. Email instructor: [instructor email]

---

**Last Updated**: May 29, 2026
**Document Version**: 1.0
