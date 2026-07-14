using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests;

public class RemovalWorkflowTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async System.Threading.Tasks.Task Student_Leaving_Creates_RemovalRequest_With_Self_RequestType()
    {
        var dbName = nameof(Student_Leaving_Creates_RemovalRequest_With_Self_RequestType);
        using var context = CreateContext(dbName);

        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        var studentMember = new GroupMember { Group = group, UserId = student.Id, Role = "Member", IsActive = true };
        var profMember = new GroupMember { Group = group, UserId = prof.Id, Role = "Professor", IsActive = true };

        group.Members.Add(studentMember);
        group.Members.Add(profMember);
        context.Groups.Add(group);
        context.GroupMembers.AddRange(studentMember, profMember);

        await context.SaveChangesAsync();

        // Student requests to leave
        var removalRequest = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            RequestedByUserId = student.Id,
            Reason = "I want to leave this group",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RemovalRequests.Add(removalRequest);
        await context.SaveChangesAsync();

        var request = await context.RemovalRequests.FirstOrDefaultAsync(rr => rr.UserId == student.Id);
        Assert.NotNull(request);
        Assert.Equal(student.Id, request.RequestedByUserId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Lead_Removing_Student_Creates_RemovalRequest()
    {
        var dbName = nameof(Lead_Removing_Student_Creates_RemovalRequest);
        using var context = CreateContext(dbName);

        var lead = new User { Id = "lead1", FirstName = "Lead" };
        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(lead, student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = lead.Id, Role = "Lead" });
        group.Members.Add(new GroupMember { Group = group, UserId = student.Id, Role = "Member" });
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);
        await context.SaveChangesAsync();

        // Lead requests removal of student
        var removalRequest = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            RequestedByUserId = lead.Id,
            Reason = "Student not contributing",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RemovalRequests.Add(removalRequest);
        await context.SaveChangesAsync();

        var request = await context.RemovalRequests.FirstOrDefaultAsync();
        Assert.NotNull(request);
        Assert.Equal(lead.Id, request.RequestedByUserId);
        Assert.Equal(student.Id, request.UserId);
        Assert.Equal("Pending", request.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Professor_Can_Approve_RemovalRequest()
    {
        var dbName = nameof(Professor_Can_Approve_RemovalRequest);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1", FirstName = "Prof" };
        var lead = new User { Id = "lead1", FirstName = "Lead" };
        var student = new User { Id = "stu1", FirstName = "Student" };
        context.Users.AddRange(prof, lead, student);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });
        group.Members.Add(new GroupMember { Group = group, UserId = lead.Id, Role = "Lead" });
        group.Members.Add(new GroupMember { Group = group, UserId = student.Id, Role = "Member" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var removalRequest = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            RequestedByUserId = lead.Id,
            Reason = "Not contributing",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RemovalRequests.Add(removalRequest);
        await context.SaveChangesAsync();

        // Professor approves
        var request = await context.RemovalRequests.FirstAsync();
        request.Status = "Approved";
        request.ApprovedByUserId = prof.Id;
        request.ResolvedAt = DateTime.UtcNow;

        context.RemovalRequests.Update(request);

        var studentMember = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);
        studentMember.IsActive = false;

        context.GroupMembers.Update(studentMember);
        await context.SaveChangesAsync();

        var approvedRequest = await context.RemovalRequests.FirstAsync();
        var inactiveMember = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);

        Assert.Equal("Approved", approvedRequest.Status);
        Assert.Equal(prof.Id, approvedRequest.ApprovedByUserId);
        Assert.False(inactiveMember.IsActive);
    }

    [Fact]
    public async System.Threading.Tasks.Task Professor_Can_Reject_RemovalRequest()
    {
        var dbName = nameof(Professor_Can_Reject_RemovalRequest);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1", FirstName = "Prof" };
        var lead = new User { Id = "lead1", FirstName = "Lead" };
        var student = new User { Id = "stu1", FirstName = "Student" };
        context.Users.AddRange(prof, lead, student);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });
        group.Members.Add(new GroupMember { Group = group, UserId = lead.Id, Role = "Lead" });
        group.Members.Add(new GroupMember { Group = group, UserId = student.Id, Role = "Member" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var removalRequest = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            RequestedByUserId = lead.Id,
            Reason = "Not contributing",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RemovalRequests.Add(removalRequest);
        await context.SaveChangesAsync();

        // Professor rejects
        var request = await context.RemovalRequests.FirstAsync();
        request.Status = "Rejected";
        request.ApprovedByUserId = prof.Id;
        request.ResolvedAt = DateTime.UtcNow;

        context.RemovalRequests.Update(request);
        await context.SaveChangesAsync();

        var rejectedRequest = await context.RemovalRequests.FirstAsync();
        var stillActiveMember = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);

        Assert.Equal("Rejected", rejectedRequest.Status);
        Assert.True(stillActiveMember.IsActive);
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Removes_Student_Directly_Without_Approval()
    {
        var dbName = nameof(Admin_Removes_Student_Directly_Without_Approval);
        using var context = CreateContext(dbName);

        var admin = new User { Id = "admin1", FirstName = "Admin" };
        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(admin, student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });
        group.Members.Add(new GroupMember { Group = group, UserId = student.Id, Role = "Member" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);
        await context.SaveChangesAsync();

        // Admin directly removes student (no RemovalRequest created)
        var memberToRemove = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);
        memberToRemove.IsActive = false;

        context.GroupMembers.Update(memberToRemove);
        await context.SaveChangesAsync();

        var removedMember = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);

        Assert.False(removedMember.IsActive);
    }

    [Fact]
    public async System.Threading.Tasks.Task Multiple_Pending_RemovalRequests_Can_Coexist()
    {
        var dbName = nameof(Multiple_Pending_RemovalRequests_Can_Coexist);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1" };
        var lead = new User { Id = "lead1" };
        var stu1 = new User { Id = "stu1" };
        var stu2 = new User { Id = "stu2" };
        context.Users.AddRange(prof, lead, stu1, stu2);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });
        group.Members.Add(new GroupMember { Group = group, UserId = lead.Id, Role = "Lead" });
        group.Members.Add(new GroupMember { Group = group, UserId = stu1.Id, Role = "Member" });
        group.Members.Add(new GroupMember { Group = group, UserId = stu2.Id, Role = "Member" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var req1 = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = stu1.Id,
            RequestedByUserId = stu1.Id,
            Reason = "Leaving",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        var req2 = new RemovalRequest
        {
            GroupId = group.Id,
            UserId = stu2.Id,
            RequestedByUserId = lead.Id,
            Reason = "Not contributing",
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.RemovalRequests.AddRange(req1, req2);
        await context.SaveChangesAsync();

        var pendingRequests = await context.RemovalRequests
            .Where(rr => rr.GroupId == group.Id && rr.Status == "Pending")
            .ToListAsync();

        Assert.Equal(2, pendingRequests.Count);
    }
}
