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

        // === CREATE USERS ===

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

        // Create sample professor user (Demo Professor)
        var professorUser = new User
        {
            UserName = "davneet@teamsync.com",
            Email = "davneet@teamsync.com",
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

        // Create Lead Student (Team Lead)
        var leadStudent = new User
        {
            UserName = "alex.chen@teamsync.com",
            Email = "alex.chen@teamsync.com",
            FirstName = "Alex",
            LastName = "Chen",
            StudentId = "STU001",
            EmailConfirmed = true,
            IsActive = true
        };

        result = await _userManager.CreateAsync(leadStudent, "Student@123456");
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(leadStudent, "Student");
            _logger.LogInformation("Lead Student created successfully.");
        }

        // Create Regular Students
        var regularStudents = new[]
        {
            new { Email = "jordan.smith@teamsync.com", First = "Jordan", Last = "Smith", ID = "STU002" },
            new { Email = "priya.patel@teamsync.com", First = "Priya", Last = "Patel", ID = "STU003" }
        };

        var studentUsers = new List<User>();
        foreach (var student in regularStudents)
        {
            var studentUser = new User
            {
                UserName = student.Email,
                Email = student.Email,
                FirstName = student.First,
                LastName = student.Last,
                StudentId = student.ID,
                EmailConfirmed = true,
                IsActive = true
            };

            result = await _userManager.CreateAsync(studentUser, "Student@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(studentUser, "Student");
                studentUsers.Add(studentUser);
                _logger.LogInformation($"Student user {student.Email} created successfully.");
            }
        }

        await _context.SaveChangesAsync();

        // === CREATE GROUP ===
        var now = DateTime.UtcNow;
        var group = new Group
        {
            Name = "Mobile App Development - Sprint 8",
            Description = "Build a cross-platform mobile app for task management using Flutter and Firebase backend",
            CreatedById = professorUser.Id,
            CreatedAt = now.AddDays(-30),
            IsActive = true,
            JoinCode = "MOBAPP8"
        };

        _context.Groups.Add(group);
        await _context.SaveChangesAsync();

        // === ADD GROUP MEMBERS ===
        var members = new[]
        {
            new GroupMember { GroupId = group.Id, UserId = professorUser.Id, Role = "Professor", JoinedAt = now.AddDays(-30) },
            new GroupMember { GroupId = group.Id, UserId = leadStudent.Id, Role = "Lead", JoinedAt = now.AddDays(-28) }
        };

        foreach (var member in members)
        {
            _context.GroupMembers.Add(member);
        }

        // Add regular students
        foreach (var student in studentUsers)
        {
            var member = new GroupMember
            {
                GroupId = group.Id,
                UserId = student.Id,
                Role = "Student",
                JoinedAt = now.AddDays(-28)
            };
            _context.GroupMembers.Add(member);
        }

        await _context.SaveChangesAsync();

        // === CREATE TASKS WITH VARIED STATUSES ===
        var taskConfigs = new[]
        {
            // Completed tasks (show high completion in dashboard)
            new { Title = "Finalize UI Design Mockups", Desc = "Complete all Figma designs for main screens including home, task detail, user profile, and settings. Ensure responsive design for mobile devices and accessibility compliance. Include light and dark theme variations. Export design token specifications for development team. Coordinate final reviews with stakeholders and incorporate feedback on visual hierarchy, spacing, and typography.", DueDate = now.AddDays(-10), Status = "Completed", Priority = 2, AssignedTo = leadStudent.Id },
            new { Title = "Setup Firebase Project", Desc = "Initialize Firebase project with authentication and real-time database configuration. Create Firebase console project, enable Firebase Authentication (Email/Password and Google Sign-in), configure Cloud Firestore with security rules for multi-user access. Setup Firebase Storage for user profile images and attachments. Configure environment variables for API keys. Test authentication flows with test users and verify security rules for data isolation between groups.", DueDate = now.AddDays(-8), Status = "Completed", Priority = 3, AssignedTo = studentUsers[0].Id },
            new { Title = "Create Authentication Module", Desc = "Implement comprehensive login, register, and password reset flows in Flutter. Build secure password handling with bcrypt hashing. Create JWT token management for session handling. Implement refresh token architecture. Setup error handling for network failures and invalid credentials. Create biometric authentication option (fingerprint/face recognition). Build remember-me functionality with secure local storage. Test authentication with edge cases including expired sessions and concurrent logins.", DueDate = now.AddDays(-3), Status = "Completed", Priority = 1, AssignedTo = studentUsers[1].Id },

            // In-Progress tasks (shows worksheet)
            new { Title = "Build Home Screen UI", Desc = "Implement home screen with dynamic task list, filtering, and sorting capabilities. Create responsive layout that works on devices from 5\" to 7\" screens. Implement task completion percentage progress indicator and visual status indicators. Build quick filter chips for status, priority, and date range. Add search functionality with real-time filtering. Create skeleton loading states for better UX. Implement pull-to-refresh for data synchronization. Add swipe actions for quick task operations (mark complete, archive).", DueDate = now.AddDays(5), Status = "InProgress", Priority = 1, AssignedTo = leadStudent.Id },
            new { Title = "Implement Task Creation Flow", Desc = "Create comprehensive task creation form with validation and submission logic. Build form fields for title, description, due date picker, priority selection, and assignee selection. Implement real-time form validation with user-friendly error messages. Create dynamic form based on task type (standard, milestone, review). Build attachment upload functionality. Implement draft auto-save to local storage. Add bulk task creation from template. Create confirmation dialog before submission. Handle offline scenarios by queuing submissions.", DueDate = now.AddDays(7), Status = "InProgress", Priority = 1, AssignedTo = studentUsers[0].Id },

            // Pending tasks
            new { Title = "Build Task Detail View", Desc = "Implement comprehensive task detail view showing all task information, edit functionality, comment threads, and activity history. Create rich text display for task description with markdown support. Build collapsible sections for description, contributors, approval history, and discussion. Implement comment/note system with threaded replies. Create activity feed showing task status changes, assignments, and approvals. Add edit in-place functionality for assigned fields. Build related tasks carousel. Show contributor avatars and contribution hours summary. Create share and export options.", DueDate = now.AddDays(10), Status = "Pending", Priority = 2, AssignedTo = studentUsers[1].Id },
            new { Title = "Implement Real-time Sync", Desc = "Setup WebSocket connections and Firestore listeners for real-time updates across all users. Configure Cloud Functions for synchronizing task changes. Implement optimistic UI updates with conflict resolution. Build background sync service for reliable delivery. Create delta sync for bandwidth efficiency. Implement listener management to prevent memory leaks. Add error recovery with exponential backoff. Build data versioning for conflict-free synchronization. Test with simulated network failures and reconnection scenarios.", DueDate = now.AddDays(12), Status = "Pending", Priority = 2, AssignedTo = leadStudent.Id },

            // Ready for Review (shows approval workflow)
            new { Title = "Write Unit Tests", Desc = "Comprehensive test suite for authentication module achieving 80%+ code coverage. Create unit tests for login, registration, password reset, and token refresh flows. Mock Firebase calls and network requests. Build parameterized tests for edge cases (SQL injection attempts, invalid formats, boundary values). Implement integration tests with test database. Setup CI/CD pipeline to run tests on commit. Create test fixtures for consistent test data. Document test scenarios and expected behaviors. Achieve branch coverage for all conditional logic.", DueDate = now.AddDays(-1), Status = "ReadyForReview", Priority = 2, AssignedTo = studentUsers[0].Id },

            // OVERDUE TASK (shows alert on dashboard!)
            new { Title = "API Rate Limiting Implementation", Desc = "Add rate limiting to prevent abuse and DoS attacks on API endpoints. Implement token bucket algorithm for rate limiting per user and per IP address. Configure different limits for authenticated vs anonymous requests. Create dashboard for monitoring rate limit violations. Implement graceful degradation when limits exceeded with helpful error messages. Setup alerts for suspicious patterns (multiple failed attempts). Create admin interface to adjust limits dynamically. Document rate limits in API documentation. Test with load testing tools to verify effectiveness.", DueDate = now.AddDays(-5), Status = "InProgress", Priority = 3, AssignedTo = studentUsers[1].Id }
        };

        var createdTasks = new List<Models.Task>();
        foreach (var taskData in taskConfigs)
        {
            var taskItem = new Models.Task
            {
                GroupId = group.Id,
                Title = taskData.Title,
                Description = taskData.Desc,
                DueDate = taskData.DueDate,
                StartDate = taskData.DueDate.AddDays(-5),
                Status = taskData.Status,
                Priority = taskData.Priority,
                AssignedToId = taskData.AssignedTo,
                CreatedById = professorUser.Id,
                CreatedAt = now.AddDays(-25),
                UpdatedAt = now.AddDays(-1)
            };

            // Set approval workflow based on task status
            if (taskData.Status == "Completed")
            {
                // Completed tasks have full approval history
                taskItem.ReviewRequestedById = taskData.AssignedTo;
                taskItem.ReviewRequestedAt = now.AddDays(-4);
                taskItem.LeadApprovedById = leadStudent.Id;
                taskItem.LeadApprovedAt = now.AddDays(-3);
                taskItem.CompletionApprovedById = professorUser.Id;
                taskItem.CompletionApprovedAt = now.AddDays(-2);
                taskItem.ApprovalNotes = "Excellent work! All requirements met.";
            }
            else if (taskData.Status == "ReadyForReview")
            {
                // Tasks ready for review have review request but no approval yet
                taskItem.ReviewRequestedById = taskData.AssignedTo;
                taskItem.ReviewRequestedAt = now.AddDays(-1);
            }
            else if (taskData.Status == "InProgress")
            {
                // In-progress tasks may have review requested
                if (taskData.Priority == 1) // High priority tasks get reviewed
                {
                    taskItem.ReviewRequestedById = taskData.AssignedTo;
                    taskItem.ReviewRequestedAt = now.AddDays(-2);
                    taskItem.LeadApprovedById = leadStudent.Id;
                    taskItem.LeadApprovedAt = now.AddDays(-1);
                }
            }

            _context.Tasks.Add(taskItem);
            createdTasks.Add(taskItem);
        }

        await _context.SaveChangesAsync();

        // === CREATE CONTRIBUTIONS (Activity History for Charts) ===
        var random = new Random(42); // Seed for reproducibility

        var contributionDescriptions = new Dictionary<string, List<string>>
        {
            { "Finalize UI Design Mockups", new List<string>
                {
                    "Refined home screen mockup based on design system guidelines",
                    "Created responsive layouts for tablet devices",
                    "Designed dark mode theme with accessible color contrasts",
                    "Documented design decisions and component specifications"
                }
            },
            { "Setup Firebase Project", new List<string>
                {
                    "Initialized Firebase console project and enabled services",
                    "Configured authentication providers and security rules",
                    "Set up Cloud Firestore database structure for tasks collection",
                    "Implemented Firebase Storage for media uploads"
                }
            },
            { "Create Authentication Module", new List<string>
                {
                    "Implemented email/password authentication with validation",
                    "Built JWT token generation and refresh token logic",
                    "Created secure password storage with encryption",
                    "Added multi-factor authentication setup"
                }
            },
            { "Build Home Screen UI", new List<string>
                {
                    "Implemented responsive task list layout",
                    "Built filter and sort functionality UI components",
                    "Added progress indicators and status badges",
                    "Optimized rendering performance for large lists"
                }
            },
            { "Implement Task Creation Flow", new List<string>
                {
                    "Built multi-step task creation form with validation",
                    "Integrated date picker and time selection",
                    "Implemented attachment upload functionality",
                    "Added auto-save to local storage for draft recovery"
                }
            },
            { "Build Task Detail View", new List<string>
                {
                    "Created expandable sections for task metadata",
                    "Built comment thread system with nested replies",
                    "Implemented activity feed for task changes",
                    "Added edit-in-place functionality for task fields"
                }
            },
            { "Implement Real-time Sync", new List<string>
                {
                    "Configured WebSocket connection management",
                    "Implemented Firestore real-time listeners",
                    "Built conflict resolution algorithm",
                    "Added connection retry logic with exponential backoff"
                }
            },
            { "Write Unit Tests", new List<string>
                {
                    "Created authentication module unit tests",
                    "Built test fixtures for consistent test data",
                    "Implemented edge case testing for validation",
                    "Achieved 82% code coverage for auth module"
                }
            },
            { "API Rate Limiting Implementation", new List<string>
                {
                    "Implemented token bucket algorithm",
                    "Configured rate limits per user endpoint",
                    "Built monitoring dashboard for rate limit events",
                    "Tested with load simulator for accuracy"
                }
            }
        };

        // Generate contributions for last 30 days
        foreach (var task in createdTasks)
        {
            // Skip tasks without assigned user
            if (string.IsNullOrEmpty(task.AssignedToId))
                continue;

            List<string> descriptions = new List<string>();
            if (contributionDescriptions.ContainsKey(task.Title))
            {
                descriptions = contributionDescriptions[task.Title];
            }

            if (task.Status == "Completed")
            {
                // Multiple contributions per completed task (3-5 entries)
                int contributionCount = random.Next(3, 6);
                for (int i = 0; i < contributionCount; i++)
                {
                    var contributionDate = now.AddDays(-random.Next(1, 20));
                    var desc = descriptions.Count > 0 ? descriptions[i % descriptions.Count] : $"Working on {task.Title} - phase {(i + 1)}";

                    var contribution = new Contribution
                    {
                        TaskId = task.Id,
                        UserId = task.AssignedToId,
                        Description = desc,
                        ContributedAt = contributionDate,
                        HoursSpent = (decimal)(random.Next(2, 8) + Math.Round(random.NextDouble(), 1)),
                        RecordedById = task.AssignedToId,
                        RecordedAt = contributionDate.AddHours(random.Next(1, 12)),
                        Source = "ManualEntry",
                        IsStudentSubmitted = true,
                        Notes = $"Completed work session - {random.Next(15, 90)} minutes of focused effort"
                    };
                    _context.Contributions.Add(contribution);
                }
            }
            else if (task.Status == "InProgress")
            {
                // Recent contributions for in-progress tasks (2-3 entries)
                int contributionCount = random.Next(2, 4);
                for (int i = 0; i < contributionCount; i++)
                {
                    var contributionDate = now.AddDays(-random.Next(0, 7));
                    var desc = descriptions.Count > 0 ? descriptions[i % descriptions.Count] : $"Progress on {task.Title} - phase {(i + 1)}";

                    var contribution = new Contribution
                    {
                        TaskId = task.Id,
                        UserId = task.AssignedToId,
                        Description = desc,
                        ContributedAt = contributionDate,
                        HoursSpent = (decimal)(random.Next(2, 7) + Math.Round(random.NextDouble(), 1)),
                        RecordedById = task.AssignedToId,
                        RecordedAt = contributionDate.AddHours(random.Next(1, 8)),
                        Source = "ManualEntry",
                        IsStudentSubmitted = true,
                        Notes = "Work in progress - tracking daily development activities"
                    };
                    _context.Contributions.Add(contribution);
                }
            }
            else if (task.Status == "ReadyForReview")
            {
                // Review task has contributions logged
                var contribution = new Contribution
                {
                    TaskId = task.Id,
                    UserId = task.AssignedToId,
                    Description = "Test suite completed and ready for review - all tests passing",
                    ContributedAt = now.AddDays(-2),
                    HoursSpent = 6.5m,
                    RecordedById = task.AssignedToId,
                    RecordedAt = now.AddDays(-2).AddHours(4),
                    Source = "ManualEntry",
                    IsStudentSubmitted = true,
                    Notes = "Code coverage achieved: 82%. Ready for peer review and integration."
                };
                _context.Contributions.Add(contribution);
            }
            else if (task.Status == "Pending")
            {
                // Pending tasks may have initial contributions or planning sessions
                if (random.Next(0, 2) == 0)
                {
                    var contribution = new Contribution
                    {
                        TaskId = task.Id,
                        UserId = task.AssignedToId,
                        Description = "Task planning and architecture design session",
                        ContributedAt = now.AddDays(-random.Next(5, 15)),
                        HoursSpent = (decimal)(random.Next(1, 4) + Math.Round(random.NextDouble(), 1)),
                        RecordedById = task.AssignedToId,
                        RecordedAt = now.AddDays(-random.Next(5, 15)).AddHours(2),
                        Source = "ManualEntry",
                        IsStudentSubmitted = true,
                        Notes = "Initial requirements gathering and technical approach documentation"
                    };
                    _context.Contributions.Add(contribution);
                }
            }
        }

        await _context.SaveChangesAsync();

        // Test: Add one manual contribution to the first completed task to verify DB works
        var firstCompletedTask = createdTasks.FirstOrDefault(t => t.Status == "Completed");
        if (firstCompletedTask != null && !string.IsNullOrEmpty(firstCompletedTask.AssignedToId))
        {
            var testContribution = new Contribution
            {
                TaskId = firstCompletedTask.Id,
                UserId = firstCompletedTask.AssignedToId,
                Description = "Initialized Firebase console project and enabled services",
                ContributedAt = now.AddDays(-15),
                HoursSpent = 5.5m,
                RecordedById = firstCompletedTask.AssignedToId,
                RecordedAt = now.AddDays(-15).AddHours(4),
                Source = "ManualEntry",
                IsStudentSubmitted = true,
                Notes = "Initial Firebase setup - 4.5 hours"
            };
            _context.Contributions.Add(testContribution);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added test contribution");
        }

        var savedContributions = await _context.Contributions.CountAsync();
        _logger.LogInformation($"Demo test data seeded successfully: {savedContributions} contributions in database.");
    }
}
