using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

namespace TeamSync.Tests.Integration;

public class ContributionIntegrationTests
{
    [Fact]
    public void AddContribution_CreatesHistory_RecordInserted()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        try
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(connection)
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                // Ensure database schema created
                context.Database.EnsureCreated();

                // Seed minimal data: user, group, task
                var user = new User { Id = "u1", FirstName = "Test", LastName = "User", Email = "test@example.com" };
                context.Users.Add(user);

                var group = new Group { Name = "G1", CreatedById = user.Id, JoinCode = "ABC-123", CreatedAt = DateTime.UtcNow, IsActive = true };
                context.Groups.Add(group);
                context.SaveChanges();

                var task = new Task { GroupId = group.Id, Title = "T1", CreatedById = user.Id, Status = "Completed", CreatedAt = DateTime.UtcNow };
                context.Tasks.Add(task);
                context.SaveChanges();

                var contribution = new Contribution
                {
                    TaskId = task.Id,
                    UserId = user.Id,
                    Description = "Work done",
                    HoursSpent = 2.5m,
                    RecordedById = user.Id,
                    RecordedAt = DateTime.UtcNow
                };

                context.Contributions.Add(contribution);
                context.SaveChanges();

                var history = new ContributionHistory
                {
                    ContributionId = contribution.Id,
                    Action = "Created",
                    PerformedById = user.Id,
                    PerformedAt = DateTime.UtcNow,
                    Changes = "Created via integration test"
                };

                context.ContributionHistories.Add(history);
                context.SaveChanges();

                Assert.Equal(1, context.Contributions.Count());
                Assert.Equal(1, context.ContributionHistories.Count());
            }
        }
        finally
        {
            connection.Close();
            connection.Dispose();
        }
    }
}
