using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TeamSync.Data;
using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Services;

/// <summary>
/// Background service that runs periodically to check for:
/// 1. Tasks with approaching deadlines
/// 2. Recent task status changes
/// Creates notifications and broadcasts them via SignalR in real-time.
/// </summary>
public class DeadlineCheckService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeadlineCheckService> _logger;
    private readonly TimeSpan _interval;

    public DeadlineCheckService(
        IServiceProvider serviceProvider,
        ILogger<DeadlineCheckService> logger,
        IConfiguration config)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Get interval from config, default to 1 hour
        var intervalMinutes = config.GetValue<int>("NotificationSettings:CheckIntervalMinutes", 60);
        _interval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DeadlineCheckService is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckDeadlinesAndStatusChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in DeadlineCheckService");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("DeadlineCheckService is stopping.");
    }

    private async Task CheckDeadlinesAndStatusChangesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

        // Get configured deadline thresholds
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var deadlineThresholds = config.GetSection("NotificationSettings:DeadlineThresholdDays")
            .Get<int[]>() ?? new[] { 7, 3, 1 };

        _logger.LogInformation(
            "Starting deadline check with thresholds (days): {Thresholds}",
            string.Join(", ", deadlineThresholds));

        // Check for approaching deadlines
        await CheckApproachingDeadlinesAsync(
            context, notificationService, deadlineThresholds, cancellationToken);

        // Check for recent status changes
        await CheckRecentStatusChangesAsync(context, notificationService, cancellationToken);

        _logger.LogInformation("Deadline and status check completed.");
    }

    private async Task CheckApproachingDeadlinesAsync(
        ApplicationDbContext context,
        NotificationService notificationService,
        int[] deadlineThresholds,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        // Track tasks already notified in this cycle to prevent duplicates across thresholds
        var tasksAlreadyNotified = new HashSet<(string userId, int taskId)>();

        foreach (var thresholdDays in deadlineThresholds)
        {
            var thresholdDate = now.AddDays(thresholdDays);
            var startOfDay = thresholdDate.Date;
            var endOfDay = startOfDay.AddDays(1);

            // Find tasks with due dates in this threshold window
            var tasksWithApproachingDeadlines = await context.Tasks
                .Where(t =>
                    t.DueDate.HasValue &&
                    t.DueDate >= startOfDay &&
                    t.DueDate < endOfDay &&
                    !t.ArchivedAt.HasValue &&
                    t.Status != "Completed")
                .Include(t => t.AssignedTo)
                .Include(t => t.CreatedBy)
                .Include(t => t.Group)
                .ThenInclude(g => g.Members)
                .ToListAsync(cancellationToken);

            foreach (var task in tasksWithApproachingDeadlines)
            {
                var userIdsToNotify = GetNotificationRecipientsForTask(task);

                foreach (var userId in userIdsToNotify)
                {
                    // Check if already notified in this cycle or recently
                    var notificationKey = (userId, task.Id);
                    if (tasksAlreadyNotified.Contains(notificationKey))
                    {
                        continue; // Already notified this user about this task in this cycle
                    }

                    var hasRecentNotification = await notificationService.HasRecentNotificationAsync(
                        userId,
                        "DeadlineReminder",
                        task.Id,
                        withinLastMinutes: 1440); // Don't re-notify within 24 hours

                    if (!hasRecentNotification)
                    {
                        var dueDate = task.DueDate!.Value.Date;
                        var daysRemaining = (int)(dueDate - now.Date).TotalDays;
                        var message = daysRemaining switch
                        {
                            0 => $"Task '{task.Title}' is due today!",
                            1 => $"Task '{task.Title}' is due tomorrow!",
                            _ => $"Task '{task.Title}' is due in {daysRemaining} days."
                        };

                        await notificationService.CreateNotificationAsync(
                            userId,
                            "DeadlineReminder",
                            message,
                            task.Id);

                        tasksAlreadyNotified.Add(notificationKey);

                        _logger.LogInformation(
                            "Created deadline reminder notification for user {UserId} on task {TaskId}",
                            userId, task.Id);
                    }
                }
            }
        }
    }

    private async Task CheckRecentStatusChangesAsync(
        ApplicationDbContext context,
        NotificationService notificationService,
        CancellationToken cancellationToken)
    {
        // Get tasks updated in the last check interval + some buffer
        var recentTime = DateTime.UtcNow.AddMinutes(-(int)(_interval.TotalMinutes + 5));

        var recentlyUpdatedTasks = await context.Tasks
            .Where(t =>
                t.UpdatedAt.HasValue &&
                t.UpdatedAt >= recentTime &&
                !t.ArchivedAt.HasValue)
            .Include(t => t.AssignedTo)
            .Include(t => t.CreatedBy)
            .Include(t => t.Group)
            .ThenInclude(g => g.Members)
            .ToListAsync(cancellationToken);

        foreach (var task in recentlyUpdatedTasks)
        {
            // Only notify on important status changes
            if (ShouldNotifyOnStatusChange(task.Status))
            {
                var userIdsToNotify = GetNotificationRecipientsForTask(task);

                foreach (var userId in userIdsToNotify)
                {
                    var hasRecentNotification = await notificationService.HasRecentNotificationAsync(
                        userId,
                        "StatusChange",
                        task.Id,
                        withinLastMinutes: 60);

                    if (!hasRecentNotification)
                    {
                        var message = $"Task '{task.Title}' status changed to: {task.Status}";

                        await notificationService.CreateNotificationAsync(
                            userId,
                            "StatusChange",
                            message,
                            task.Id);

                        _logger.LogInformation(
                            "Created status change notification for user {UserId} on task {TaskId}",
                            userId, task.Id);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Determine who should be notified about a task.
    /// Uses HashSet to automatically deduplicate recipients if they have multiple roles.
    /// Includes: assigned users, task creator, and group leads.
    /// </summary>
    private HashSet<string> GetNotificationRecipientsForTask(ModelTask task)
    {
        // HashSet automatically deduplicates if the same user appears in multiple roles
        var recipients = new HashSet<string>();

        // Add assigned user
        if (!string.IsNullOrEmpty(task.AssignedToId))
        {
            recipients.Add(task.AssignedToId);
        }

        // Add task creator
        if (!string.IsNullOrEmpty(task.CreatedById))
        {
            recipients.Add(task.CreatedById);
        }

        // Add group lead
        if (task.Group != null && task.Group.Members != null)
        {
            foreach (var member in task.Group.Members)
            {
                if (member.Role == "Lead")
                {
                    recipients.Add(member.UserId);
                }
            }
        }

        // Note: Professor notifications would require User role check
        // This is handled via the task creator or group context

        return recipients;
    }

    /// <summary>
    /// Determine if a status change warrants notification.
    /// </summary>
    private bool ShouldNotifyOnStatusChange(string status)
    {
        // Only notify for significant status changes
        return status switch
        {
            "Completed" => true,
            "In Review" => true,
            "Approved" => true,
            "Rejected" => true,
            _ => false
        };
    }
}
