using Microsoft.Extensions.DependencyInjection;
using TeamSync.Services;

namespace TeamSync.BackgroundJobs;

/// <summary>
/// Background service for sending weekly email digests.
/// Runs periodically and checks if it's time to send digest emails to users.
/// </summary>
public class WeeklyDigestBackgroundService : BackgroundService
{
    private readonly ILogger<WeeklyDigestBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1); // Check every hour

    public WeeklyDigestBackgroundService(
        ILogger<WeeklyDigestBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async System.Threading.Tasks.Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Weekly Digest Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDigestEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in weekly digest background service");
            }

            // Wait before next check
            await System.Threading.Tasks.Task.Delay(_checkInterval, stoppingToken);
        }

        _logger.LogInformation("Weekly Digest Background Service stopped");
    }

    private async System.Threading.Tasks.Task ProcessDigestEmailsAsync(CancellationToken cancellationToken)
    {
        using (var scope = _serviceProvider.CreateScope())
        {
            var digestEmailService = scope.ServiceProvider.GetRequiredService<IDigestEmailService>();

            try
            {
                // Send all pending digest emails
                await digestEmailService.SendAllDigestsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing digest emails");
            }
        }
    }
}
