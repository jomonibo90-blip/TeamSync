using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

namespace TeamSync.Tests
{
    public class ContributionTests
    {
        [Fact]
        public void CanCreateAndPersistContribution_WithAuditFields()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "ContribTestDb")
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                var task = new Task { Title = "T1", CreatedAt = DateTime.UtcNow };
                context.Tasks.Add(task);
                context.SaveChanges();

                var contribution = new Contribution
                {
                    TaskId = task.Id,
                    UserId = "user1",
                    Description = "Completed task: T1",
                    ContributedAt = DateTime.UtcNow,
                    HoursSpent = 2.5m,
                    RecordedById = "approver1",
                    RecordedAt = DateTime.UtcNow,
                    Source = "TaskFinalization",
                    Notes = "Good work"
                };

                context.Contributions.Add(contribution);
                context.SaveChanges();

                var db = context.Contributions.FirstOrDefault(c => c.TaskId == task.Id);
                Assert.NotNull(db);
                Assert.Equal(2.5m, db.HoursSpent);
                Assert.Equal("approver1", db.RecordedById);
                Assert.Equal("TaskFinalization", db.Source);
            }
        }
    }
}
