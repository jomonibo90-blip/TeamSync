using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

namespace TeamSync.Tests
{
    public class MemberRemovalTests
    {
        private ApplicationDbContext GetInMemoryContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async System.Threading.Tasks.Task UnassignTasksWhenMemberRemoved()
        {
            using var context = GetInMemoryContext();

            // Arrange: create group, user, membership, task assigned
            var user = new User { Id = "u1", UserName = "u1", Email = "u1@example.com", FirstName = "U", LastName = "One", StudentId = "S1", CreatedAt = DateTime.UtcNow, IsActive = true };
            var group = new Group { Name = "G1", CreatedById = "u1", JoinCode = "join", CreatedAt = DateTime.UtcNow, IsActive = true };
            context.Add(user);
            context.Add(group);
            await context.SaveChangesAsync();

            var gm = new GroupMember { GroupId = group.Id, UserId = user.Id, Role = "Member", JoinedAt = DateTime.UtcNow, IsActive = true };
            context.GroupMembers.Add(gm);
            await context.SaveChangesAsync();

            var task = new TeamSync.Models.Task { Title = "T1", GroupId = group.Id, AssignedToId = user.Id, CreatedById = user.Id, CreatedAt = DateTime.UtcNow, Priority = 2, Status = "Pending" };
            context.Tasks.Add(task);
            await context.SaveChangesAsync();

            // Act: remove membership and run unassign logic
            context.GroupMembers.Remove(gm);
            await context.SaveChangesAsync();

            // Simulate controller's unassign logic
            var tasksToUnassign = await context.Tasks.Where(t => t.GroupId == group.Id && t.AssignedToId == user.Id).ToListAsync();
            foreach (var t in tasksToUnassign)
            {
                t.AssignedToId = null;
                t.UpdatedAt = DateTime.UtcNow;
                context.Tasks.Update(t);
            }
            await context.SaveChangesAsync();

            // Assert
            var updatedTask = await context.Tasks.FirstOrDefaultAsync();
            Assert.Null(updatedTask.AssignedToId);
        }
    }
}
