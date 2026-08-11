using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.Services;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Controllers;

/// <summary>
/// Test controller for generating sample data and triggering digest emails.
/// WARNING: This should only be used in development environments.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IDigestEmailService _digestEmailService;
    private readonly ILogger<TestController> _logger;

    public TestController(
        ApplicationDbContext context,
        UserManager<User> userManager,
        IDigestEmailService digestEmailService,
        ILogger<TestController> logger)
    {
        _context = context;
        _userManager = userManager;
        _digestEmailService = digestEmailService;
        _logger = logger;
    }

    /// <summary>
    /// Generate sample data and send a test digest email.
    /// Creates test users, groups, tasks, and notifications from the past 4 days.
    /// </summary>
    [HttpPost("generate-and-send-digest")]
    public async Task<IActionResult> GenerateAndSendDigest()
    {
        try
        {
            _logger.LogInformation("Starting test data generation and digest email trigger");

            // Step 1: Create or find test user
            var testUserEmail = "jomonibo@gmail.com";
            var testUser = await _userManager.FindByEmailAsync(testUserEmail);

            if (testUser == null)
            {
                testUser = new User
                {
                    UserName = "teststudent",
                    Email = testUserEmail,
                    FirstName = "Test",
                    LastName = "Student",
                    StudentId = "TEST001"
                };
                var result = await _userManager.CreateAsync(testUser, "TestPass123!");
                if (!result.Succeeded)
                {
                    return BadRequest($"Failed to create test user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
                await _userManager.AddToRoleAsync(testUser, "Student");
            }

            // Step 2: Ensure alert preference exists
            var alertPref = await _context.AlertPreferences.FirstOrDefaultAsync(ap => ap.UserId == testUser.Id);
            if (alertPref == null)
            {
                alertPref = new AlertPreference
                {
                    UserId = testUser.Id,
                    NotificationFrequency = "Weekly",
                    ReceiveTaskAssignmentAlerts = true,
                    ReceiveApprovalRejectionAlerts = true,
                    ReceiveStatusChangeAlerts = true
                };
                _context.AlertPreferences.Add(alertPref);
                await _context.SaveChangesAsync();
            }

            // Step 3: Create or find a test professor
            var professorEmail = "professor@university.edu";
            var professor = await _userManager.FindByEmailAsync(professorEmail);
            if (professor == null)
            {
                professor = new User
                {
                    UserName = "testprofessor",
                    Email = professorEmail,
                    FirstName = "Test",
                    LastName = "Professor",
                    StudentId = "PROF001"
                };
                var result = await _userManager.CreateAsync(professor, "ProfPass123!");
                if (!result.Succeeded)
                {
                    professor = await _userManager.FindByEmailAsync(professorEmail);
                }
                else
                {
                    await _userManager.AddToRoleAsync(professor, "Professor");
                }
            }

            // Step 4: Create test group
            var testGroup = new Group
            {
                Name = "Test Project Group",
                Description = "Test group for digest email demonstration",
                JoinCode = Guid.NewGuid().ToString().Substring(0, 8).ToUpper(),
                CreatedById = professor.Id,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.Groups.Add(testGroup);
            await _context.SaveChangesAsync();

            // Step 5: Add members to group
            var leadMember = new GroupMember
            {
                GroupId = testGroup.Id,
                UserId = professor.Id,
                Role = "Lead",
                JoinedAt = DateTime.UtcNow
            };
            var studentMember = new GroupMember
            {
                GroupId = testGroup.Id,
                UserId = testUser.Id,
                Role = "Member",
                JoinedAt = DateTime.UtcNow
            };
            _context.GroupMembers.Add(leadMember);
            _context.GroupMembers.Add(studentMember);
            await _context.SaveChangesAsync();

            // Step 6: Create sample tasks from the past 4 days with various statuses
            var modelTasks = new List<Models.Task>();
            var statusArray = new[] { "To Do", "In Progress", "Completed", "Ready for Review" };

            for (int i = 0; i < 4; i++)
            {
                var daysAgo = i + 1;
                var modelTask = new Models.Task
                {
                    Title = $"Sample Task {i + 1}: Complete API Documentation",
                    Description = $"Comprehensive documentation for the REST API endpoints. This task was created {daysAgo} day(s) ago.",
                    Status = statusArray[i % statusArray.Length],
                    Priority = i % 2 == 0 ? 3 : 2, // 1-5 scale, 3=High, 2=Medium
                    GroupId = testGroup.Id,
                    AssignedToId = testUser.Id,
                    CreatedById = professor.Id,
                    CreatedAt = DateTime.UtcNow.AddDays(-daysAgo),
                    DueDate = DateTime.UtcNow.AddDays(3),
                    StartDate = DateTime.UtcNow.AddDays(-daysAgo)
                };
                modelTasks.Add(modelTask);
                _context.Tasks.Add(modelTask);
            }
            await _context.SaveChangesAsync();

            // Step 7: Create notifications for these tasks
            var notificationTypes = new[] { "TaskAssigned", "TaskStatusChanged", "TaskCompleted", "ReviewRequested" };
            foreach (var modelTask in modelTasks)
            {
                var notification = new Notification
                {
                    UserId = testUser.Id,
                    TaskId = modelTask.Id,
                    Type = notificationTypes[new Random().Next(notificationTypes.Length)],
                    Message = $"Task '{modelTask.Title}' - {modelTask.Status}",
                    IsRead = false,
                    CreatedAt = modelTask.CreatedAt
                };
                _context.Notifications.Add(notification);
            }
            await _context.SaveChangesAsync();

            // Step 8: Add task notes with file attachments for realism
            var notes = new List<TaskNote>();
            var noteContents = new[]
            {
                "Great progress on this task! The implementation looks solid.",
                "Please review the attached documentation and provide feedback.",
                "All requirements have been met. Ready for final review.",
                "Excellent work! This is a comprehensive solution."
            };

            for (int i = 0; i < modelTasks.Count; i++)
            {
                var note = new TaskNote
                {
                    TaskId = modelTasks[i].Id,
                    UserId = professor.Id,
                    Content = noteContents[i],
                    CreatedAt = DateTime.UtcNow.AddDays(-(4 - i)),
                    UpdatedAt = null
                };
                _context.TaskNotes.Add(note);
                notes.Add(note);
            }
            await _context.SaveChangesAsync();

            _logger.LogInformation($"Created {modelTasks.Count} sample tasks with notifications");

            // Step 9: Send digest email to test user
            _logger.LogInformation($"Triggering digest email send for user: {testUser.Email}");
            await _digestEmailService.SendUserDigestAsync(testUser.Id);

            return Ok(new
            {
                success = true,
                message = "Test data generated and digest email sent successfully!",
                details = new
                {
                    testUserEmail = testUserEmail,
                    groupName = testGroup.Name,
                    tasksCreated = modelTasks.Count,
                    notificationsCreated = modelTasks.Count,
                    taskDetails = modelTasks.Select(t => new
                    {
                        title = t.Title,
                        status = t.Status,
                        priority = t.Priority,
                        createdAt = t.CreatedAt,
                        assignedTo = testUser.FullName
                    }).ToList()
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating test data or sending digest email");
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }

    /// <summary>
    /// Get the status of the last digest email sent to the test user.
    /// </summary>
    [HttpGet("digest-status")]
    public async Task<IActionResult> GetDigestStatus()
    {
        try
        {
            var testUserEmail = "jomonibo@gmail.com";
            var testUser = await _userManager.FindByEmailAsync(testUserEmail);

            if (testUser == null)
            {
                return NotFound("Test user not found");
            }

            var alertPref = await _context.AlertPreferences.FirstOrDefaultAsync(ap => ap.UserId == testUser.Id);
            var recentNotifications = await _context.Notifications
                .Where(n => n.UserId == testUser.Id)
                .OrderByDescending(n => n.CreatedAt)
                .Take(10)
                .ToListAsync();

            return Ok(new
            {
                testUserEmail = testUserEmail,
                lastDigestSentAt = alertPref?.LastDigestSentAt,
                recentNotificationsCount = recentNotifications.Count,
                recentNotifications = recentNotifications.Select(n => new
                {
                    message = n.Message,
                    type = n.Type,
                    createdAt = n.CreatedAt
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting digest status");
            return StatusCode(500, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Clean up test data (optional).
    /// </summary>
    [HttpDelete("cleanup")]
    public async Task<IActionResult> Cleanup()
    {
        try
        {
            var testGroup = await _context.Groups.FirstOrDefaultAsync(g => g.Name == "Test Project Group");
            if (testGroup != null)
            {
                // Remove related data
                var members = await _context.GroupMembers.Where(m => m.GroupId == testGroup.Id).ToListAsync();
                _context.GroupMembers.RemoveRange(members);

                var modelTasks = await _context.Tasks.Where(t => t.GroupId == testGroup.Id).ToListAsync();
                foreach (var modelTask in modelTasks)
                {
                    var notes = await _context.TaskNotes.Where(n => n.TaskId == modelTask.Id).ToListAsync();
                    _context.TaskNotes.RemoveRange(notes);

                    var attachments = await _context.FileAttachments.Where(fa => fa.TaskNote.TaskId == modelTask.Id).ToListAsync();
                    _context.FileAttachments.RemoveRange(attachments);
                }
                _context.Tasks.RemoveRange(modelTasks);
                _context.Groups.Remove(testGroup);

                await _context.SaveChangesAsync();
                return Ok(new { message = "Test data cleaned up successfully" });
            }

            return NotFound("Test data not found");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up test data");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
