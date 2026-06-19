# Entity Framework Core Migrations Guide
**Team accountability, simplified**

## Overview
This document explains how to manage database migrations in TeamSync using Entity Framework Core.

## Initial Setup (Already Completed)

The application is configured to automatically apply migrations on startup via the `DbInitializerService`. This means:
- Database will be created automatically if it doesn't exist
- All pending migrations will be applied automatically
- Sample data will be seeded on first run

## Creating New Migrations

When you modify the data models and need to create a new migration:

### Using Package Manager Console (Visual Studio)
```powershell
Add-Migration MigrationName
Update-Database
```

### Using .NET CLI
```bash
dotnet ef migrations add MigrationName
dotnet ef database update
```

### Example: Adding a New Column
1. Modify your model in `Models/` directory
2. Run: `Add-Migration AddNewColumn`
3. Review the generated migration file in `Migrations/` directory
4. Run: `Update-Database`

## Viewing Migration Status

To see pending migrations:
```bash
dotnet ef migrations list
```

## Removing Last Migration (If Not Applied)

If you created a migration but haven't applied it yet:
```bash
Remove-Migration
```

## Checking Database Schema

To view the current database schema:
```bash
dotnet ef dbcontext info
```

## Important Notes

1. **Automatic Migrations on Startup**: The application calls `await dbInitializer.InitializeAsync()` in `Program.cs`, which automatically applies all pending migrations.

2. **Migration Files Location**: All migration files are stored in the `Migrations/` folder.

3. **Design-Time DbContext Factory**: If needed, you can implement `IDesignTimeDbContextFactory<ApplicationDbContext>` to specify DbContext options for CLI tools.

4. **Team Collaboration**: Always commit migration files to the repository so team members can apply them automatically.

## Troubleshooting

### Build Fails: "The type or namespace name 'DbInitializerService' could not be found"
- Ensure using statement is added: `using TeamSync.Services;`
- Check that the file is saved and project is built

### Database Already Exists but Migrations Aren't Applied
1. Open Package Manager Console
2. Run: `Update-Database -Force` (WARNING: This can cause data loss in dev)

### Need to Rollback to Previous Migration
```bash
Update-Database -Migration MigrationName
```

## Best Practices

1. ✅ Always create a migration when you modify models
2. ✅ Name migrations descriptively (e.g., `AddGroupContributionTracking`)
3. ✅ Review generated migration files for accuracy
4. ✅ Commit migration files to source control
5. ✅ Test migrations in development before deploying
6. ❌ Don't modify migration files manually after they've been applied

## Useful Commands Reference

| Task | Package Manager Console | .NET CLI |
|------|-------------------------|----------|
| Add Migration | `Add-Migration Name` | `dotnet ef migrations add Name` |
| Update Database | `Update-Database` | `dotnet ef database update` |
| List Migrations | `Get-Migration` | `dotnet ef migrations list` |
| Remove Migration | `Remove-Migration` | `dotnet ef migrations remove` |
| Script Migration | `Script-Migration` | `dotnet ef migrations script` |
| Drop Database | `Drop-Database` | `dotnet ef database drop` |

## Environment-Specific Configuration

For production environments, you may want to:
1. Create a separate `appsettings.Production.json`
2. Disable automatic migrations
3. Apply migrations manually during deployment

Example `appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=your-production-server;Database=TeamSync;..."
  }
}
```

Then modify `Program.cs` to conditionally run migrations only in development:
```csharp
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbInitializer = scope.ServiceProvider.GetRequiredService<DbInitializerService>();
        await dbInitializer.InitializeAsync();
    }
}
