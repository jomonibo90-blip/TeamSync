using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.Services;

namespace TeamSync.Controllers;

/// <summary>
/// Temporary seeding controller for demo data (remove after use)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SeedController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<SeedController> _logger;
    private readonly IDigestEmailService _digestEmailService;
    private readonly IEmailService _emailService;

    public SeedController(ApplicationDbContext context, UserManager<User> userManager, RoleManager<IdentityRole> roleManager, ILogger<SeedController> logger, IDigestEmailService digestEmailService, IEmailService emailService)
    {
        _context = context;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
        _digestEmailService = digestEmailService;
        _emailService = emailService;
    }

    /// <summary>
    /// POST /api/seed/demo - Seeds fresh demo data
    /// </summary>
    [HttpPost("demo")]
    public async Task<IActionResult> SeedDemoData()
    {
        try
        {
            // Create roles first if they don't exist
            var roles = new[] { "Admin", "Professor", "Student" };
            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    await _roleManager.CreateAsync(new IdentityRole(role));
                    _logger.LogInformation($"Created role: {role}");
                }
            }

            // Clear existing data with better error handling
            try
            {
                _context.Contributions.RemoveRange(_context.Contributions);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear Contributions: {ex.Message}"); }

            try
            {
                _context.ChatMessages.RemoveRange(_context.ChatMessages);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear ChatMessages: {ex.Message}"); }

            try
            {
                _context.FileAttachments.RemoveRange(_context.FileAttachments);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear FileAttachments: {ex.Message}"); }

            try
            {
                _context.Notifications.RemoveRange(_context.Notifications);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear Notifications: {ex.Message}"); }

            try
            {
                _context.AlertPreferences.RemoveRange(_context.AlertPreferences);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear AlertPreferences: {ex.Message}"); }

            try
            {
                _context.Tasks.RemoveRange(_context.Tasks);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear Tasks: {ex.Message}"); }

            try
            {
                _context.GroupMembers.RemoveRange(_context.GroupMembers);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear GroupMembers: {ex.Message}"); }

            try
            {
                _context.Groups.RemoveRange(_context.Groups);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear Groups: {ex.Message}"); }

            try
            {
                _context.UserLogins.RemoveRange(_context.UserLogins);
                _context.UserClaims.RemoveRange(_context.UserClaims);
                _context.UserRoles.RemoveRange(_context.UserRoles);
                _context.UserTokens.RemoveRange(_context.UserTokens);
                _context.Users.RemoveRange(_context.Users);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex) { _logger.LogWarning($"Could not clear Users: {ex.Message}"); }

            _logger.LogInformation("Cleared existing data");

            // Create Professor
            var professor = new User
            {
                UserName = "davneet@teamsync.com",
                Email = "davneet@teamsync.com",
                FirstName = "Davneet",
                LastName = "Chawla",
                EmailConfirmed = true,
                IsActive = true
            };
            var result = await _userManager.CreateAsync(professor, "Professor@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(professor, "Professor");
                _logger.LogInformation("Created Professor: Davneet Chawla");
            }
            else
            {
                _logger.LogError($"Failed to create professor: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            }

            // Create Student 1 (Lead)
            var jordan = new User
            {
                UserName = "jordan@teamsync.com",
                Email = "jordan@teamsync.com",
                FirstName = "Jordan",
                LastName = "Lead",
                StudentId = "STU001",
                EmailConfirmed = true,
                IsActive = true
            };
            result = await _userManager.CreateAsync(jordan, "Student@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(jordan, "Student");
                _logger.LogInformation("Created Student 1: Jordan");
            }

            // Create Student 2
            var steve = new User
            {
                UserName = "steve@teamsync.com",
                Email = "steve@teamsync.com",
                FirstName = "Steve",
                LastName = "Smith",
                StudentId = "STU002",
                EmailConfirmed = true,
                IsActive = true
            };
            result = await _userManager.CreateAsync(steve, "Student@123456");
            if (result.Succeeded)
            {
                await _userManager.AddToRoleAsync(steve, "Student");
                _logger.LogInformation("Created Student 2: Steve");
            }

            await _context.SaveChangesAsync();

            // Create Group
            var now = DateTime.UtcNow;
            var group = new Group
            {
                Name = "Mobile App Development - Sprint 8",
                Description = "Build a cross-platform mobile app for task management using Flutter and Firebase backend",
                CreatedById = professor.Id,
                CreatedAt = now.AddDays(-30),
                IsActive = true,
                JoinCode = "MOBAPP8"
            };
            _context.Groups.Add(group);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Created Group");

            // Add Members
            _context.GroupMembers.AddRange(
                new GroupMember { GroupId = group.Id, UserId = professor.Id, Role = "Professor", JoinedAt = now.AddDays(-30) },
                new GroupMember { GroupId = group.Id, UserId = jordan.Id, Role = "Lead", JoinedAt = now.AddDays(-28) },
                new GroupMember { GroupId = group.Id, UserId = steve.Id, Role = "Student", JoinedAt = now.AddDays(-28) }
            );
            await _context.SaveChangesAsync();
            _logger.LogInformation("Added group members");

            // Create 9 Tasks
            var taskConfigs = new[]
            {
                // Completed
                new { Title = "Finalize UI Design Mockups", Desc = "Complete all Figma designs", DueDate = now.AddDays(-10), Status = "Completed", Priority = 2, AssignedTo = jordan.Id },
                new { Title = "Setup Firebase Project", Desc = "Initialize Firebase with auth", DueDate = now.AddDays(-8), Status = "Completed", Priority = 3, AssignedTo = steve.Id },
                new { Title = "Create Authentication Module", Desc = "Login, register, password reset", DueDate = now.AddDays(-3), Status = "Completed", Priority = 1, AssignedTo = jordan.Id },

                // In-Progress
                new { Title = "Build Home Screen UI", Desc = "Implement home screen with task list", DueDate = now.AddDays(5), Status = "InProgress", Priority = 1, AssignedTo = jordan.Id },
                new { Title = "Implement Task Creation Flow", Desc = "Form, validation, submission logic", DueDate = now.AddDays(7), Status = "InProgress", Priority = 1, AssignedTo = steve.Id },

                // Pending
                new { Title = "Build Task Detail View", Desc = "Task details, edit, delete, comments", DueDate = now.AddDays(10), Status = "Pending", Priority = 2, AssignedTo = steve.Id },
                new { Title = "Implement Real-time Sync", Desc = "WebSocket/Firestore listeners", DueDate = now.AddDays(12), Status = "Pending", Priority = 2, AssignedTo = jordan.Id },

                // Ready for Review
                new { Title = "Write Unit Tests", Desc = "Test auth module, 80%+ coverage", DueDate = now.AddDays(-1), Status = "ReadyForReview", Priority = 2, AssignedTo = steve.Id },

                // OVERDUE - RED ALERT!
                new { Title = "API Rate Limiting Implementation", Desc = "Add rate limiting to prevent abuse", DueDate = now.AddDays(-5), Status = "InProgress", Priority = 3, AssignedTo = steve.Id }
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
                    CreatedById = professor.Id,
                    CreatedAt = now.AddDays(-25),
                    UpdatedAt = now.AddDays(-1)
                };

                if (taskData.Status == "Completed")
                {
                    taskItem.CompletionApprovedById = professor.Id;
                    taskItem.CompletionApprovedAt = now.AddDays(-2);
                }

                _context.Tasks.Add(taskItem);
                createdTasks.Add(taskItem);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("Created 9 tasks");

            // Add Contributions (with better error handling)
            try
            {
                var random = new Random(42);
                foreach (var task in createdTasks)
                {
                    // Validate task has an ID
                    if (task.Id <= 0)
                    {
                        _logger.LogWarning($"Task {task.Title} has invalid ID {task.Id}, skipping contributions");
                        continue;
                    }

                    // Validate AssignedToId exists
                    if (string.IsNullOrEmpty(task.AssignedToId))
                    {
                        _logger.LogWarning($"Task {task.Title} has no AssignedToId, skipping contributions");
                        continue;
                    }

                    if (task.Status == "Completed")
                    {
                        for (int i = 0; i < random.Next(2, 5); i++)
                        {
                            var contributionDate = now.AddDays(-random.Next(1, 20));
                            _context.Contributions.Add(new Contribution
                            {
                                TaskId = task.Id,
                                UserId = task.AssignedToId,
                                Description = $"Working on {task.Title}",
                                ContributedAt = contributionDate,
                                HoursSpent = (decimal)random.Next(1, 8),
                                RecordedById = task.AssignedToId,
                                RecordedAt = contributionDate.AddHours(random.Next(1, 12)),
                                Source = "ManualEntry",
                                IsStudentSubmitted = true
                            });
                        }
                    }
                    else if (task.Status == "InProgress")
                    {
                        for (int i = 0; i < random.Next(1, 3); i++)
                        {
                            var contributionDate = now.AddDays(-random.Next(0, 5));
                            _context.Contributions.Add(new Contribution
                            {
                                TaskId = task.Id,
                                UserId = task.AssignedToId,
                                Description = $"Progress on {task.Title}",
                                ContributedAt = contributionDate,
                                HoursSpent = (decimal)random.Next(2, 6),
                                RecordedById = task.AssignedToId,
                                RecordedAt = contributionDate.AddHours(random.Next(1, 8)),
                                Source = "ManualEntry",
                                IsStudentSubmitted = true
                            });
                        }
                    }
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Added contributions");
            }
            catch (Exception contribEx)
            {
                _logger.LogError($"Error adding contributions: {contribEx.Message}. Proceeding without contributions.");
                // Don't fail entirely - we have the core data seeded
            }

            return Ok(new 
            { 
                message = "Demo data seeded successfully!",
                professor = "davneet@teamsync.com",
                student1 = "jim@teamsync.com",
                student2 = "steve@teamsync.com",
                password = "Professor@123456 (Prof) / Student@123456 (Students)",
                group = "Mobile App Development - Sprint 8",
                tasks = 9,
                contributions = "10+",
                ready = "Login now and record! 🎬"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error seeding data: {ex.Message}\n{ex.StackTrace}");
            return BadRequest(new { error = ex.Message, details = ex.StackTrace });
        }
    }

    /// <summary>
    /// POST /api/seed/setup-digest-emails - Setup alert preferences for demo users and enable weekly digests
    /// </summary>
    [HttpPost("setup-digest-emails")]
    public async Task<IActionResult> SetupDigestEmails()
    {
        try
        {
            var jordan = await _context.Users.FirstOrDefaultAsync(u => u.Email == "jordan@teamsync.com");
            var steve = await _context.Users.FirstOrDefaultAsync(u => u.Email == "steve@teamsync.com");

            if (jordan == null || steve == null)
            {
                return BadRequest(new { error = "Demo users not found. Run /api/seed/demo first." });
            }

            // Count contributions for each user to determine who has more data
            var jordanContributions = await _context.Contributions.CountAsync(c => c.UserId == jordan.Id);
            var steveContributions = await _context.Contributions.CountAsync(c => c.UserId == steve.Id);

            var targetUser = jordanContributions >= steveContributions ? jordan : steve;

            _logger.LogInformation($"Selected {targetUser.FirstName} for digest ({jordanContributions} vs {steveContributions} contributions)");

            // Create or update AlertPreferences for target user
            var existingPref = await _context.AlertPreferences.FirstOrDefaultAsync(ap => ap.UserId == targetUser.Id);

            if (existingPref != null)
            {
                _context.AlertPreferences.Remove(existingPref);
                await _context.SaveChangesAsync();
            }

            var digestPreference = new AlertPreference
            {
                UserId = targetUser.Id,
                NotificationFrequency = "Weekly",
                DigestDayOfWeek = (int)DateTime.UtcNow.DayOfWeek, // Send today if demo timing is now
                DigestHourUtc = DateTime.UtcNow.Hour + 1, // Send next hour
                ReceiveTaskAssignmentAlerts = true,
                ReceiveApprovalRejectionAlerts = true,
                ReceiveStatusChangeAlerts = true,
                ReceiveCommentAlerts = true,
                ReceiveGroupAlerts = true
            };

            _context.AlertPreferences.Add(digestPreference);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created AlertPreference for {targetUser.Email} - will send digest at {digestPreference.DigestHourUtc}:00 UTC");

            return Ok(new
            {
                message = "Digest email setup complete!",
                targetUser = new { email = targetUser.Email, name = targetUser.FirstName },
                contributions = jordanContributions >= steveContributions ? jordanContributions : steveContributions,
                digestSchedule = new
                {
                    dayOfWeek = (DayOfWeek)digestPreference.DigestDayOfWeek,
                    hourUtc = digestPreference.DigestHourUtc,
                    note = "Digest will be sent by background service at the scheduled time, or use /api/seed/send-digest-now for immediate delivery"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting up digest emails");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/seed/create-test-notifications - Create some test notifications for digest testing
    /// </summary>
    [HttpPost("create-test-notifications")]
    public async Task<IActionResult> CreateTestNotifications()
    {
        try
        {
            var user = await _context.Users
                .Include(u => u.AlertPreference)
                .FirstOrDefaultAsync(u => u.AlertPreference != null && u.AlertPreference.NotificationFrequency == "Weekly");

            if (user == null)
            {
                return BadRequest(new { error = "No user with weekly digest preference found." });
            }

            // Create some test notifications from the past week
            var notifications = new List<Notification>
            {
                new Notification
                {
                    UserId = user.Id,
                    Type = "TaskAssignment",
                    Message = "You have been assigned to task: Build Home Screen UI",
                    CreatedAt = DateTime.UtcNow.AddDays(-5),
                    IsRead = false
                },
                new Notification
                {
                    UserId = user.Id,
                    Type = "StatusChange",
                    Message = "Task status changed: Implementation Task - Finalize UI Design Mockups has been marked as Completed",
                    CreatedAt = DateTime.UtcNow.AddDays(-4),
                    IsRead = false
                },
                new Notification
                {
                    UserId = user.Id,
                    Type = "ApprovalRequested",
                    Message = "Approval requested for: Create Authentication Module - Please review and approve completion",
                    CreatedAt = DateTime.UtcNow.AddDays(-3),
                    IsRead = false
                },
                new Notification
                {
                    UserId = user.Id,
                    Type = "Comment",
                    Message = "New comment on Build Home Screen UI: Great progress on the UI implementation!",
                    CreatedAt = DateTime.UtcNow.AddDays(-2),
                    IsRead = false
                },
                new Notification
                {
                    UserId = user.Id,
                    Type = "TaskAssignment",
                    Message = "You have been assigned to task: Implement Real-time Sync",
                    CreatedAt = DateTime.UtcNow.AddHours(-12),
                    IsRead = false
                }
            };

            _context.Notifications.AddRange(notifications);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created {notifications.Count} test notifications for {user.Email}");

            return Ok(new
            {
                message = "Test notifications created successfully!",
                notificationsCreated = notifications.Count,
                user = user.Email,
                note = "Now you can call /api/seed/send-digest-now to send a digest email with this test data"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating test notifications");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// POST /api/seed/send-digest-now?email=your-email@example.com - Manually trigger digest email (optional email parameter to override)
    /// </summary>
    [HttpPost("send-digest-now")]
    public async Task<IActionResult> SendDigestNow([FromQuery] string? email = null)
    {
        try
        {
            // Find the user with AlertPreference set to Weekly
            var user = await _context.Users
                .Include(u => u.AlertPreference)
                .FirstOrDefaultAsync(u => u.AlertPreference != null && u.AlertPreference.NotificationFrequency == "Weekly");

            if (user == null)
            {
                return BadRequest(new { error = "No user with weekly digest preference found. Run /api/seed/setup-digest-emails first." });
            }

            // Check if user has any notifications/alerts to send
            var recentNotifications = await _context.Notifications
                .Where(n => n.UserId == user.Id && n.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            if (recentNotifications == 0)
            {
                return BadRequest(new { error = "No alerts from the past week for this user." });
            }

            // Send the digest email
            await _digestEmailService.SendUserDigestAsync(user.Id);

            var recipientEmail = email ?? user.Email;

            // If email override provided, also send a copy to that email
            if (!string.IsNullOrWhiteSpace(email) && email != user.Email)
            {
                var alerts = await _context.Notifications
                    .Include(n => n.Task)
                    .Where(n => n.UserId == user.Id && n.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                    .OrderByDescending(n => n.CreatedAt)
                    .ToListAsync();

                if (alerts.Any())
                {
                    var filteredAlerts = alerts.Where(a =>
                    {
                        return a.Type switch
                        {
                            "TaskAssignment" => user.AlertPreference?.ReceiveTaskAssignmentAlerts ?? true,
                            "ApprovalRequested" => user.AlertPreference?.ReceiveApprovalRejectionAlerts ?? true,
                            "ApprovalRejected" => user.AlertPreference?.ReceiveApprovalRejectionAlerts ?? true,
                            "StatusChange" => user.AlertPreference?.ReceiveStatusChangeAlerts ?? true,
                            "Comment" => user.AlertPreference?.ReceiveCommentAlerts ?? true,
                            "GroupMember" => user.AlertPreference?.ReceiveGroupAlerts ?? true,
                            _ => true
                        };
                    }).ToList();

                    if (filteredAlerts.Any())
                    {
                        // Generate the full digest email for the secondary recipient
                        var (htmlContent, plainTextContent) = GenerateDigestEmail(user, filteredAlerts);
                        await _emailService.SendEmailAsync(email, "TeamSync Weekly Digest", htmlContent, plainTextContent);
                        _logger.LogInformation($"Copy of digest also sent to {email}");
                    }
                }
            }

            _logger.LogInformation($"Manual digest email sent to {user.Email}" + (email != null && email != user.Email ? $" and {email}" : ""));

            return Ok(new
            {
                message = "Digest email sent successfully!",
                recipientPrimary = user.Email,
                recipientSecondary = email != null && email != user.Email ? email : null,
                alertsIncluded = recentNotifications,
                note = "Check your inbox (and spam folder) for the weekly digest email"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending digest email");
            return BadRequest(new { error = ex.Message, details = ex.StackTrace });
        }
    }

    // Helper method to generate digest email (copied from DigestEmailService for demo purposes)
    private (string htmlContent, string plainTextContent) GenerateDigestEmail(User user, List<Notification> alerts)
    {
        var html = new System.Text.StringBuilder();
        var plainText = new System.Text.StringBuilder();

        // HTML Header with Material 3 Design
        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html>");
        html.AppendLine("<head>");
        html.AppendLine("<meta charset=\"UTF-8\">");
        html.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        html.AppendLine("<style>");
        html.AppendLine("* { margin: 0; padding: 0; box-sizing: border-box; }");
        html.AppendLine("body { font-family: 'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; line-height: 1.6; color: #181c20; background-color: #f8f9fa; }");
        html.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; background-color: #ffffff; }");
        html.AppendLine(".header { background: linear-gradient(135deg, #0057cd 0%, #0d6efd 100%); color: #ffffff; padding: 40px 30px; border-radius: 12px; margin-bottom: 32px; box-shadow: 0 3px 6px rgba(0, 0, 0, 0.15), 0 2px 4px rgba(0, 0, 0, 0.12); }");
        html.AppendLine(".header h1 { margin: 0 0 12px 0; font-size: 32px; font-weight: 600; font-family: 'Poppins', sans-serif; letter-spacing: -0.5px; }");
        html.AppendLine(".header p { margin: 0; font-size: 14px; opacity: 0.95; }");
        html.AppendLine(".content-section { margin-bottom: 32px; }");
        html.AppendLine(".section-title { font-size: 18px; font-weight: 600; color: #181c20; margin-bottom: 16px; padding-bottom: 8px; border-bottom: 2px solid #e8f0ff; }");
        html.AppendLine(".alert-item { border-left: 4px solid #0057cd; padding: 16px; margin-bottom: 12px; background-color: #f1f4f9; border-radius: 8px; box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12), 0 1px 2px rgba(0, 0, 0, 0.24); transition: all 250ms ease; }");
        html.AppendLine(".alert-item:hover { background-color: #e8f0ff; box-shadow: 0 3px 6px rgba(0, 0, 0, 0.15), 0 2px 4px rgba(0, 0, 0, 0.12); }");
        html.AppendLine(".alert-type { display: inline-block; background-color: #0057cd; color: #ffffff; padding: 6px 12px; border-radius: 6px; font-size: 11px; font-weight: 600; margin-bottom: 8px; letter-spacing: 0.5px; }");
        html.AppendLine(".alert-type.success { background-color: #198754; }");
        html.AppendLine(".alert-type.warning { background-color: #ffc107; color: #181c20; }");
        html.AppendLine(".alert-type.error { background-color: #ba1a1a; }");
        html.AppendLine(".alert-type.info { background-color: #0dcaf0; }");
        html.AppendLine(".alert-message { font-size: 14px; margin: 8px 0; color: #181c20; line-height: 1.5; }");
        html.AppendLine(".alert-time { font-size: 12px; color: #424655; margin-top: 8px; }");
        html.AppendLine(".summary-box { background-color: #e8f5e9; border-left: 4px solid #198754; padding: 16px; border-radius: 8px; margin-bottom: 24px; }");
        html.AppendLine(".summary-box p { color: #1b5e20; margin: 0; font-size: 14px; }");
        html.AppendLine(".footer { border-top: 1px solid #dee2e6; padding-top: 24px; margin-top: 32px; font-size: 12px; color: #424655; text-align: center; }");
        html.AppendLine(".footer a { color: #0057cd; text-decoration: none; font-weight: 500; }");
        html.AppendLine(".footer a:hover { text-decoration: underline; }");
        html.AppendLine(".divider { height: 1px; background-color: #dee2e6; margin: 24px 0; }");
        html.AppendLine("</style>");
        html.AppendLine("</head>");
        html.AppendLine("<body>");
        html.AppendLine("<div class=\"container\">");

        // HTML Body
        html.AppendLine("<div class=\"header\">");
        html.AppendLine($"<h1>TeamSync Weekly Digest</h1>");
        html.AppendLine($"<p>Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} – {DateTime.UtcNow:MMM dd, yyyy}</p>");
        html.AppendLine("</div>");

        html.AppendLine($"<p style=\"font-size: 16px; margin-bottom: 16px; color: #181c20;\">Hi <strong>{user.FirstName}</strong>,</p>");
        html.AppendLine($"<p style=\"font-size: 14px; margin-bottom: 20px; color: #424655;\">Here's your weekly summary of alerts and activities from the past week.</p>");

        // Summary Box
        html.AppendLine("<div class=\"summary-box\">");
        html.AppendLine($"<p><strong>📊 Total Alerts:</strong> {alerts.Count} new alerts</p>");
        html.AppendLine("</div>");

        // Plain Text Header
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine("                 TEAMSYNC WEEKLY DIGEST");
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine();
        plainText.AppendLine($"Week of {DateTime.UtcNow.AddDays(-7):MMM dd, yyyy} – {DateTime.UtcNow:MMM dd, yyyy}");
        plainText.AppendLine();
        plainText.AppendLine($"Hi {user.FirstName},");
        plainText.AppendLine();
        plainText.AppendLine("Here's your weekly summary of alerts and activities from the past week.");
        plainText.AppendLine();
        plainText.AppendLine($"📊 Total Alerts: {alerts.Count} new alerts");
        plainText.AppendLine();

        // Group alerts by type
        var groupedAlerts = alerts.GroupBy(a => a.Type);
        foreach (var group in groupedAlerts)
        {
            html.AppendLine("<div class=\"content-section\">");
            html.AppendLine($"<h2 class=\"section-title\">{FormatAlertType(group.Key)}</h2>");
            plainText.AppendLine($"{FormatAlertType(group.Key)}");
            plainText.AppendLine(new string('─', 50));

            foreach (var alert in group)
            {
                var alertTypeClass = GetAlertTypeClass(alert.Type);
                html.AppendLine("<div class=\"alert-item\">");
                html.AppendLine($"<div class=\"alert-type {alertTypeClass}\">{FormatAlertType(alert.Type)}</div>");
                html.AppendLine($"<div class=\"alert-message\">{System.Web.HttpUtility.HtmlEncode(alert.Message)}</div>");
                html.AppendLine($"<div class=\"alert-time\">📅 {alert.CreatedAt.ToLocalTime():MMM dd, yyyy 'at' HH:mm}</div>");
                html.AppendLine("</div>");

                plainText.AppendLine($"• {alert.Message}");
                plainText.AppendLine($"  Time: {alert.CreatedAt.ToLocalTime():MMM dd, yyyy HH:mm}");
                plainText.AppendLine();
            }

            html.AppendLine("</div>");
        }

        // Footer
        html.AppendLine("<div class=\"divider\"></div>");
        html.AppendLine("<div class=\"footer\">");
        html.AppendLine("<p style=\"margin-bottom: 12px; color: #424655;\">You're receiving this because you have subscribed to weekly digest emails.</p>");
        html.AppendLine("<p><a href=\"#\">Manage Preferences</a> | <a href=\"#\">Unsubscribe</a></p>");
        html.AppendLine("</div>");

        html.AppendLine("</div>");
        html.AppendLine("</body>");
        html.AppendLine("</html>");

        // Plain Text Footer
        plainText.AppendLine();
        plainText.AppendLine("═══════════════════════════════════════════════════════");
        plainText.AppendLine("You're receiving this because you have subscribed to weekly digest emails.");
        plainText.AppendLine("To manage your preferences, visit your account settings in TeamSync.");
        plainText.AppendLine("═══════════════════════════════════════════════════════");

        return (html.ToString(), plainText.ToString());
    }

    private string GetAlertTypeClass(string alertType)
    {
        return alertType switch
        {
            "StatusChange" => "success",
            "ApprovalRejected" => "error",
            "ApprovalRequested" => "warning",
            "Comment" => "info",
            _ => ""
        };
    }

    private string FormatAlertType(string type)
    {
        return type switch
        {
            "TaskAssignment" => "📋 Task Assignment",
            "ApprovalRequested" => "⏳ Approval Requested",
            "ApprovalRejected" => "❌ Approval Rejected",
            "StatusChange" => "🔄 Status Change",
            "Comment" => "💬 Comments",
            "GroupMember" => "👥 Group Changes",
            _ => type
        };
    }

    /// <summary>
    /// POST /api/seed/send-jordan-digest - Send one-week digest email directly to jomonibo@gmail.com
    /// </summary>
    [HttpPost("send-jordan-digest")]
    public async Task<IActionResult> SendJordanDigest()
    {
        try
        {
            var jordan = await _context.Users
                .Include(u => u.AlertPreference)
                .FirstOrDefaultAsync(u => u.Email == "jordan@teamsync.com");

            if (jordan == null)
            {
                return BadRequest(new { error = "Jordan user not found. Run /api/seed/demo first." });
            }

            // Ensure Jordan has AlertPreference set
            var alertPref = jordan.AlertPreference;
            if (alertPref == null)
            {
                alertPref = new AlertPreference
                {
                    UserId = jordan.Id,
                    NotificationFrequency = "Weekly",
                    ReceiveTaskAssignmentAlerts = true,
                    ReceiveApprovalRejectionAlerts = true,
                    ReceiveStatusChangeAlerts = true,
                    ReceiveCommentAlerts = true,
                    ReceiveGroupAlerts = true
                };
                _context.AlertPreferences.Add(alertPref);
                await _context.SaveChangesAsync();
            }

            // Check if Jordan has any notifications from the past week
            var recentNotifications = await _context.Notifications
                .Where(n => n.UserId == jordan.Id && n.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .CountAsync();

            if (recentNotifications == 0)
            {
                return BadRequest(new { error = "No alerts from the past week for Jordan. Data may need to be seeded." });
            }

            // Get alerts for the digest
            var alerts = await _context.Notifications
                .Include(n => n.Task)
                .Where(n => n.UserId == jordan.Id && n.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // Generate email content
            var (htmlContent, plainTextContent) = GenerateDigestEmail(jordan, alerts);

            // Save email as HTML file for verification
            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "emails");
            Directory.CreateDirectory(uploadsDir);
            var emailFileName = $"digest_{jordan.Id}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.html";
            var emailFilePath = Path.Combine(uploadsDir, emailFileName);
            await System.IO.File.WriteAllTextAsync(emailFilePath, htmlContent);

            _logger.LogInformation($"Email HTML saved to: {emailFilePath}");

            // Try to send via email service
            bool emailSent = false;
            string emailError = null;

            try
            {
                await _emailService.SendEmailAsync("jomonibo@gmail.com", "TeamSync Weekly Digest - Jordan's Activities", htmlContent, plainTextContent);
                emailSent = true;
                _logger.LogInformation($"One-week digest email sent to jomonibo@gmail.com with {recentNotifications} notifications from Jordan");
            }
            catch (Exception emailEx)
            {
                emailError = emailEx.Message;
                _logger.LogWarning(emailEx, $"SMTP Error - but email HTML generated and saved. Error: {emailEx.Message}");
            }

            var response = new
            {
                message = emailSent 
                    ? "✅ Email sent to jomonibo@gmail.com!" 
                    : "⚠️ Email HTML generated (saved for review) but SMTP delivery failed",
                recipient = "jomonibo@gmail.com",
                notificationsIncluded = recentNotifications,
                subject = "TeamSync Weekly Digest - Jordan's Activities",
                timestamp = DateTime.UtcNow,
                emailSent = emailSent,
                smtpError = emailError,
                previewUrl = $"/uploads/emails/{emailFileName}",
                note = emailSent 
                    ? "Check your inbox" 
                    : "Email HTML saved to preview. SMTP configuration needs review."
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing digest request");
            return BadRequest(new { error = ex.Message, details = ex.StackTrace });
        }
    }
}
