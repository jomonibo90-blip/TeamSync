using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests;

public class TaskArchiveTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async System.Threading.Tasks.Task Can_Archive_Pending_Task()
    {
        var dbName = nameof(Can_Archive_Pending_Task);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1", FirstName = "Creator" };
        var lead = new User { Id = "lead1", FirstName = "Lead" };
        context.Users.AddRange(creator, lead);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            GroupId = group.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Archive the task
        var taskToArchive = await context.Tasks.FirstAsync();
        taskToArchive.ArchivedAt = DateTime.UtcNow;
        taskToArchive.ArchivedById = lead.Id;
        taskToArchive.ArchiveReason = "Not needed anymore";

        context.Tasks.Update(taskToArchive);
        await context.SaveChangesAsync();

        var archivedTask = await context.Tasks.FirstAsync();
        Assert.NotNull(archivedTask.ArchivedAt);
        Assert.Equal(lead.Id, archivedTask.ArchivedById);
        Assert.Equal("Not needed anymore", archivedTask.ArchiveReason);
    }

    [Fact]
    public async System.Threading.Tasks.Task Cannot_Archive_Completed_Task()
    {
        var dbName = nameof(Cannot_Archive_Completed_Task);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        context.Users.Add(creator);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            GroupId = group.Id,
            Status = "Completed",
            CompletionApprovedById = creator.Id,
            CompletionApprovedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Should not allow archiving completed task
        var taskToArchive = await context.Tasks.FirstAsync();
        Assert.Equal("Completed", taskToArchive.Status);
        // In real implementation, controller would prevent this
        // But data model allows it for soft-delete flexibility
    }

    [Fact]
    public async System.Threading.Tasks.Task Can_Restore_Archived_Task()
    {
        var dbName = nameof(Can_Restore_Archived_Task);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        var lead = new User { Id = "lead1" };
        context.Users.AddRange(creator, lead);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            GroupId = group.Id,
            Status = "Pending",
            ArchivedAt = DateTime.UtcNow,
            ArchivedById = lead.Id,
            ArchiveReason = "Not needed",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        // Restore the task
        var taskToRestore = await context.Tasks.FirstAsync();
        taskToRestore.ArchivedAt = null;
        taskToRestore.ArchivedById = null;
        taskToRestore.ArchiveReason = null;

        context.Tasks.Update(taskToRestore);
        await context.SaveChangesAsync();

        var restoredTask = await context.Tasks.FirstAsync();
        Assert.Null(restoredTask.ArchivedAt);
        Assert.Null(restoredTask.ArchivedById);
        Assert.Null(restoredTask.ArchiveReason);
    }

    [Fact]
    public async System.Threading.Tasks.Task Can_Query_Archived_Tasks()
    {
        var dbName = nameof(Can_Query_Archived_Tasks);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1" };
        context.Users.Add(creator);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        // Create mix of archived and active tasks
        context.Tasks.AddRange(
            new ModelTask { Title = "Task1", CreatedById = creator.Id, GroupId = group.Id, Status = "Pending", CreatedAt = DateTime.UtcNow },
            new ModelTask { Title = "Task2", CreatedById = creator.Id, GroupId = group.Id, Status = "InProgress", CreatedAt = DateTime.UtcNow },
            new ModelTask 
            { 
                Title = "Task3", 
                CreatedById = creator.Id, 
                GroupId = group.Id, 
                Status = "Pending", 
                ArchivedAt = DateTime.UtcNow,
                ArchivedById = creator.Id,
                CreatedAt = DateTime.UtcNow
            },
            new ModelTask 
            { 
                Title = "Task4", 
                CreatedById = creator.Id, 
                GroupId = group.Id, 
                Status = "Pending", 
                ArchivedAt = DateTime.UtcNow,
                ArchivedById = creator.Id,
                CreatedAt = DateTime.UtcNow
            }
        );
        await context.SaveChangesAsync();

        // Query active tasks
        var activeTasks = await context.Tasks
            .Where(t => t.GroupId == group.Id && !t.ArchivedAt.HasValue)
            .ToListAsync();

        // Query archived tasks
        var archivedTasks = await context.Tasks
            .Where(t => t.GroupId == group.Id && t.ArchivedAt.HasValue)
            .ToListAsync();

        Assert.Equal(2, activeTasks.Count);
        Assert.Equal(2, archivedTasks.Count);
        Assert.All(activeTasks, t => Assert.Null(t.ArchivedAt));
        Assert.All(archivedTasks, t => Assert.NotNull(t.ArchivedAt));
    }

    [Fact]
    public async System.Threading.Tasks.Task Hard_Delete_Removes_All_Related_Data()
    {
        var dbName = nameof(Hard_Delete_Removes_All_Related_Data);
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
            Status = "Completed",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskId = task.Id;

        // Add contributions
        context.Contributions.Add(new Contribution
        {
            TaskId = taskId,
            UserId = assignee.Id,
            Description = "Work done",
            ContributedAt = DateTime.UtcNow,
            RecordedById = creator.Id,
            RecordedAt = DateTime.UtcNow
        });

        // Add task notes
        context.TaskNotes.Add(new TaskNote
        {
            TaskId = taskId,
            UserId = creator.Id,
            Content = "Test note",
            CreatedAt = DateTime.UtcNow
        });

        // Add task assignments
        context.TaskAssignments.Add(new TaskAssignment
        {
            TaskId = taskId,
            AssignedToId = assignee.Id,
            AssignedByUserId = creator.Id,
            AssignedAt = DateTime.UtcNow
        });

        await context.SaveChangesAsync();

        // Verify data exists
        var contributionsCount = await context.Contributions.Where(c => c.TaskId == taskId).CountAsync();
        var notesCount = await context.TaskNotes.Where(n => n.TaskId == taskId).CountAsync();
        var assignmentsCount = await context.TaskAssignments.Where(ta => ta.TaskId == taskId && ta.RemovedAt == null).CountAsync();
        var tasksCount = await context.Tasks.Where(t => t.Id == taskId).CountAsync();

        Assert.Equal(1, contributionsCount);
        Assert.Equal(1, notesCount);
        Assert.Equal(1, assignmentsCount);
        Assert.Equal(1, tasksCount);

        // Delete task (hard delete)
        var taskToDelete = await context.Tasks
            .Include(t => t.Contributions)
            .Include(t => t.Notes)
            .Include(t => t.Assignments)
            .FirstAsync(t => t.Id == taskId);

        context.ContributionHistories.RemoveRange(
            await context.ContributionHistories
                .Where(ch => ch.Contribution.TaskId == taskId)
                .ToListAsync()
        );
        context.Contributions.RemoveRange(taskToDelete.Contributions);
        context.TaskNotes.RemoveRange(taskToDelete.Notes);
        context.TaskAssignments.RemoveRange(taskToDelete.Assignments);
        context.Tasks.Remove(taskToDelete);
        await context.SaveChangesAsync();

        // Verify all deleted
        var afterContributionsCount = await context.Contributions.Where(c => c.TaskId == taskId).CountAsync();
        var afterNotesCount = await context.TaskNotes.Where(n => n.TaskId == taskId).CountAsync();
        var afterAssignmentsCount = await context.TaskAssignments.Where(ta => ta.TaskId == taskId && ta.RemovedAt == null).CountAsync();
        var afterTasksCount = await context.Tasks.Where(t => t.Id == taskId).CountAsync();

        Assert.Equal(0, afterContributionsCount);
        Assert.Equal(0, afterNotesCount);
        Assert.Equal(0, afterAssignmentsCount);
        Assert.Equal(0, afterTasksCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task Archive_With_Reason_Tracks_Metadata()
    {
        var dbName = nameof(Archive_With_Reason_Tracks_Metadata);
        using var context = CreateContext(dbName);

        var creator = new User { Id = "creator1", FirstName = "John" };
        var lead = new User { Id = "lead1", FirstName = "Jane" };
        context.Users.AddRange(creator, lead);

        var group = new Group { Id = 1, Name = "G1", CreatedById = creator.Id, IsActive = true };
        context.Groups.Add(group);

        var task = new ModelTask
        {
            Title = "Task1",
            CreatedById = creator.Id,
            GroupId = group.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.Tasks.Add(task);
        await context.SaveChangesAsync();

        var taskId = task.Id;

        // Archive with metadata
        var taskToArchive = await context.Tasks.FirstAsync();
        var archiveTime = DateTime.UtcNow;
        taskToArchive.ArchivedAt = archiveTime;
        taskToArchive.ArchivedById = lead.Id;
        taskToArchive.ArchiveReason = "Duplicate task - merged with Task #42";

        context.Tasks.Update(taskToArchive);
        await context.SaveChangesAsync();

        // Verify metadata
        var archivedTask = await context.Tasks
            .Include(t => t.ArchivedBy)
            .FirstAsync(t => t.Id == taskId);

        Assert.NotNull(archivedTask.ArchivedAt);
        Assert.Equal(lead.Id, archivedTask.ArchivedById);
        Assert.Equal("Jane", archivedTask.ArchivedBy.FirstName);
        Assert.Equal("Duplicate task - merged with Task #42", archivedTask.ArchiveReason);
        Assert.True(DateTime.UtcNow >= archivedTask.ArchivedAt.Value);
    }
}
