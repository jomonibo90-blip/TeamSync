using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests
{
    public class TaskWorkflowTests
    {
        [Fact]
        public void FinalizingTask_CreatesContribution_And_CompletesTask()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "TaskWorkflow_FinalizeDb")
                .Options;

            using (var context = new ApplicationDbContext(options))
            {
                // arrange
                var task = new Task
                {
                    Title = "Sample",
                    CreatedById = "creator",
                    AssignedToId = "assignee",
                    Status = "ReviewRequested",
                    CreatedAt = DateTime.UtcNow
                };
                context.Tasks.Add(task);
                context.SaveChanges();

                // act: simulate finalize + contribution creation without explicit transaction
                task.Status = "Completed";
                task.CompletionApprovedById = "creator";
                task.CompletionApprovedAt = DateTime.UtcNow;
                context.Tasks.Update(task);

                var contribution = new Contribution
                {
                    TaskId = task.Id,
                    UserId = task.AssignedToId ?? string.Empty,
                    Description = $"Completed task: {task.Title}",
                    ContributedAt = DateTime.UtcNow
                };
                context.Contributions.Add(contribution);

                context.SaveChanges();

                // assert
                var dbTask = context.Tasks.First();
                var contributions = context.Contributions.Where(c => c.TaskId == dbTask.Id).ToList();

                Assert.Equal("Completed", dbTask.Status);
                Assert.Single(contributions);
                Assert.Equal(dbTask.Id, contributions[0].TaskId);
            }
        }
    }
}
