using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests;

public class TaskStatusTransitionTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Workflow_Pending_To_InProgress()
    {
        var dbName = nameof(Task_Workflow_Pending_To_InProgress);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1", FirstName = "Creator" };
        var assignee = new User { Id = "assignee1", FirstName = "Assignee" };
        context.Users.AddRange(creator, assignee);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "InProgress";
        context.Tasks.Update(taskToUpdate);
        await context.SaveChangesAsync();

        var updatedTask = await context.Tasks.FirstAsync();
        Assert.Equal("InProgress", updatedTask.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Workflow_InProgress_To_ReviewRequested()
    {
        var dbName = nameof(Task_Workflow_InProgress_To_ReviewRequested);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        context.Users.AddRange(creator, assignee);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "InProgress",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "ReviewRequested";
        taskToUpdate.ReviewRequestedById = assignee.Id;
        taskToUpdate.ReviewRequestedAt = DateTime.UtcNow;

        context.Tasks.Update(taskToUpdate);
        await context.SaveChangesAsync();

        var updatedTask = await context.Tasks.FirstAsync();
        Assert.Equal("ReviewRequested", updatedTask.Status);
        Assert.Equal(assignee.Id, updatedTask.ReviewRequestedById);
        Assert.NotNull(updatedTask.ReviewRequestedAt);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Workflow_ReviewRequested_To_LeadApproved()
    {
        var dbName = nameof(Task_Workflow_ReviewRequested_To_LeadApproved);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        var lead = new User { Id = "lead1" };
        context.Users.AddRange(creator, assignee, lead);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = lead.Id, Role = "Lead" });
        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "ReviewRequested",
            ReviewRequestedById = assignee.Id,
            ReviewRequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "LeadApproved";
        taskToUpdate.LeadApprovedById = lead.Id;
        taskToUpdate.LeadApprovedAt = DateTime.UtcNow;

        context.Tasks.Update(taskToUpdate);
        await context.SaveChangesAsync();

        var updatedTask = await context.Tasks.FirstAsync();
        Assert.Equal("LeadApproved", updatedTask.Status);
        Assert.Equal(lead.Id, updatedTask.LeadApprovedById);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Workflow_LeadApproved_To_Completed_Creates_Contribution()
    {
        var dbName = nameof(Task_Workflow_LeadApproved_To_Completed_Creates_Contribution);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        var lead = new User { Id = "lead1" };
        context.Users.AddRange(creator, assignee, lead);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "LeadApproved",
            LeadApprovedById = lead.Id,
            LeadApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "Completed";
        taskToUpdate.CompletionApprovedById = creator.Id;
        taskToUpdate.CompletionApprovedAt = DateTime.UtcNow;

        context.Tasks.Update(taskToUpdate);

        var contribution = new Contribution
        {
            TaskId = taskToUpdate.Id,
            UserId = assignee.Id,
            Description = $"Completed task: {taskToUpdate.Title}",
            ContributedAt = DateTime.UtcNow,
            HoursSpent = 3.0m,
            Source = "TaskFinalization",
            RecordedById = creator.Id,
            RecordedAt = DateTime.UtcNow
        };

        context.Contributions.Add(contribution);
        await context.SaveChangesAsync();

        var completedTask = await context.Tasks.FirstAsync();
        var contribution_record = await context.Contributions.FirstAsync();

        Assert.Equal("Completed", completedTask.Status);
        Assert.Equal(assignee.Id, contribution_record.UserId);
        Assert.Equal(3.0m, contribution_record.HoursSpent);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Cannot_Skip_Workflow_Steps()
    {
        var dbName = nameof(Task_Cannot_Skip_Workflow_Steps);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        context.Users.AddRange(creator, assignee);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "Completed";
        context.Tasks.Update(taskToUpdate);
        await context.SaveChangesAsync();

        var updatedTask = await context.Tasks.FirstAsync();
        Assert.Equal("Completed", updatedTask.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_Can_Be_Rejected_From_ReviewRequested()
    {
        var dbName = nameof(Task_Can_Be_Rejected_From_ReviewRequested);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        context.Users.AddRange(creator, assignee);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "ReviewRequested",
            ReviewRequestedById = assignee.Id,
            ReviewRequestedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskToUpdate = await context.Tasks.FirstAsync();
        taskToUpdate.Status = "Rejected";

        context.Tasks.Update(taskToUpdate);
        await context.SaveChangesAsync();

        var rejectedTask = await context.Tasks.FirstAsync();
        Assert.Equal("Rejected", rejectedTask.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Task_With_StartDate_Can_Be_Assigned()
    {
        var dbName = nameof(Task_With_StartDate_Can_Be_Assigned);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var assignee = new User { Id = "assignee1" };
        context.Users.AddRange(creator, assignee);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var startDate = DateTime.UtcNow.AddDays(1);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            AssignedToId = assignee.Id,
            GroupId = group.Id,
            Status = "Pending",
            StartDate = startDate,
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var savedTask = await context.Tasks.FirstAsync();
        Assert.Equal(startDate, savedTask.StartDate);
    }
}
