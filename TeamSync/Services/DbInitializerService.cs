using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;

namespace TeamSync.Services;

public class DbInitializerService
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<DbInitializerService> _logger;

    public DbInitializerService(
        ApplicationDbContext context,
        UserManager<User> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<DbInitializerService> logger)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            // Apply pending migrations
            await _context.Database.MigrateAsync();
            _logger.LogInformation("Database migrations applied successfully.");

            // Seed roles if they don't exist
            await SeedRolesAsync();

            // Run a patch to fix old data
            await PatchLegacyRolesAsync();

            // Seed initial data
            await SeedDataAsync();

            _logger.LogInformation("Database initialization completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"An error occurred during database initialization: {ex.Message}");
            throw;
        }
    }

    private async System.Threading.Tasks.Task SeedRolesAsync()
    {
        var roles = new[] { "Admin", "Student", "Professor" };

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
                _logger.LogInformation($"Role '{role}' created.");
            }
        }
    }

    private async System.Threading.Tasks.Task PatchLegacyRolesAsync()
    {
        var legacyMembers = await _context.GroupMembers.Where(m => m.Role == "Instructor").ToListAsync();
        if (legacyMembers.Any())
        {
            foreach (var member in legacyMembers)
            {
                member.Role = "Professor";
            }
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Patched {legacyMembers.Count} legacy 'Instructor' roles to 'Professor'.");
        }
    }

    private async System.Threading.Tasks.Task SeedDataAsync()
    {
        // Check if we already have users
        if (_context.Users.Any())
        {
            _logger.LogInformation("Database already contains users. Skipping seed data.");
            return;
        }

        // Create sample admin user
        var adminUser = new User
        {
            UserName = "admin@teamsync.com",
            Email = "admin@teamsync.com",
            FirstName = "System",
            LastName = "Administrator",
            EmailConfirmed = true,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(adminUser, "Admin@123456");
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(adminUser, "Admin");
            _logger.LogInformation("Admin user created successfully.");
        }

        // Create sample professor user
        var professorUser = new User
        {
            UserName = "professor@teamsync.com",
            Email = "professor@teamsync.com",
            FirstName = "Davneet",
            LastName = "Chawla",
            EmailConfirmed = true,
            IsActive = true
        };

        result = await _userManager.CreateAsync(professorUser, "Professor@123456");
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(professorUser, "Professor");
            _logger.LogInformation("Professor user created successfully.");
        }

        // Create sample student users
        var studentEmails = new[] { "student1@teamsync.com", "student2@teamsync.com", "student3@teamsync.com" };
        var studentNames = new[] { ("John", "Doe"), ("Jane", "Smith"), ("Mike", "Johnson") };

        for (int i = 0; i < studentEmails.Length; i++)
        {
            var studentUser = new User
            {
                UserName = studentEmails[i],
                Email = studentEmails[i],
                FirstName = studentNames[i].Item1,
                LastName = studentNames[i].Item2,
                StudentId = $"STU00{i + 1}",
                EmailConfirmed = true,
                IsActive = true
            };

            result = await _userManager.CreateAsync(studentUser, "Student@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(studentUser, "Student");
                _logger.LogInformation($"Student user {studentEmails[i]} created successfully.");
            }
        }

        await _context.SaveChangesAsync();
    }
}
