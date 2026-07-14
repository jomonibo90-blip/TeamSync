using System;
using System.Linq;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using TeamSync.Data;
using TeamSync.Models;
using Xunit;

namespace TeamSync.Tests;

public class JoinRequestWorkflowTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async System.Threading.Tasks.Task Student_Can_Request_To_Join_Group_With_JoinCode()
    {
        var dbName = nameof(Student_Can_Request_To_Join_Group_With_JoinCode);
        using var context = CreateContext(dbName);

        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, JoinCode = "ABC-123", IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);
        await context.SaveChangesAsync();

        var joinRequest = new JoinRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.JoinRequests.Add(joinRequest);
        await context.SaveChangesAsync();

        var savedRequest = await context.JoinRequests.FirstAsync();
        Assert.Equal("Pending", savedRequest.Status);
        Assert.Equal(student.Id, savedRequest.UserId);
        Assert.Equal(group.Id, savedRequest.GroupId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Professor_Can_Approve_JoinRequest()
    {
        var dbName = nameof(Professor_Can_Approve_JoinRequest);
        using var context = CreateContext(dbName);

        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, JoinCode = "ABC-123", IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var joinRequest = new JoinRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.JoinRequests.Add(joinRequest);
        await context.SaveChangesAsync();

        // Professor approves
        var request = await context.JoinRequests.FirstAsync();
        request.Status = "Approved";
        request.ApprovedByUserId = prof.Id;
        request.ResolvedAt = DateTime.UtcNow;
        context.JoinRequests.Update(request);

        var newMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = student.Id,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.GroupMembers.Add(newMember);
        await context.SaveChangesAsync();

        var approvedRequest = await context.JoinRequests.FirstAsync();
        var studentMember = await context.GroupMembers
            .FirstAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);

        Assert.Equal("Approved", approvedRequest.Status);
        Assert.NotNull(approvedRequest.ResolvedAt);
        Assert.Equal("Member", studentMember.Role);
    }

    [Fact]
    public async System.Threading.Tasks.Task Professor_Can_Reject_JoinRequest()
    {
        var dbName = nameof(Professor_Can_Reject_JoinRequest);
        using var context = CreateContext(dbName);

        var student = new User { Id = "stu1", FirstName = "Student" };
        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.AddRange(student, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, JoinCode = "ABC-123", IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var joinRequest = new JoinRequest
        {
            GroupId = group.Id,
            UserId = student.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.JoinRequests.Add(joinRequest);
        await context.SaveChangesAsync();

        // Professor rejects
        var request = await context.JoinRequests.FirstAsync();
        request.Status = "Rejected";
        context.JoinRequests.Update(request);
        await context.SaveChangesAsync();

        var rejectedRequest = await context.JoinRequests.FirstAsync();
        var memberCount = await context.GroupMembers
            .CountAsync(gm => gm.UserId == student.Id && gm.GroupId == group.Id);

        Assert.Equal("Rejected", rejectedRequest.Status);
        Assert.Equal(0, memberCount);
    }

    [Fact]
    public async System.Threading.Tasks.Task Multiple_JoinRequests_Can_Be_Pending()
    {
        var dbName = nameof(Multiple_JoinRequests_Can_Be_Pending);
        using var context = CreateContext(dbName);

        var stu1 = new User { Id = "stu1" };
        var stu2 = new User { Id = "stu2" };
        var stu3 = new User { Id = "stu3" };
        var prof = new User { Id = "prof1" };
        context.Users.AddRange(stu1, stu2, stu3, prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, JoinCode = "ABC-123", IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var req1 = new JoinRequest { GroupId = group.Id, UserId = stu1.Id, Status = "Pending", CreatedAt = DateTime.UtcNow };
        var req2 = new JoinRequest { GroupId = group.Id, UserId = stu2.Id, Status = "Pending", CreatedAt = DateTime.UtcNow };
        var req3 = new JoinRequest { GroupId = group.Id, UserId = stu3.Id, Status = "Pending", CreatedAt = DateTime.UtcNow };

        context.JoinRequests.AddRange(req1, req2, req3);
        await context.SaveChangesAsync();

        var pendingRequests = await context.JoinRequests
            .Where(jr => jr.GroupId == group.Id && jr.Status == "Pending")
            .ToListAsync();

        Assert.Equal(3, pendingRequests.Count);
    }
}

public class AddMemberRequestWorkflowTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async System.Threading.Tasks.Task Professor_Can_Request_To_Add_Member_By_Email()
    {
        var dbName = nameof(Professor_Can_Request_To_Add_Member_By_Email);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1", FirstName = "Prof" };
        context.Users.Add(prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);
        await context.SaveChangesAsync();

        var addMemberRequest = new AddMemberRequest
        {
            GroupId = group.Id,
            Email = "newstudent@example.com",
            RequestedByUserId = prof.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.AddMemberRequests.Add(addMemberRequest);
        await context.SaveChangesAsync();

        var savedRequest = await context.AddMemberRequests.FirstAsync();
        Assert.Equal("Pending", savedRequest.Status);
        Assert.Equal("newstudent@example.com", savedRequest.Email);
        Assert.Equal(prof.Id, savedRequest.RequestedByUserId);
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Can_Approve_AddMemberRequest_And_Create_User()
    {
        var dbName = nameof(Admin_Can_Approve_AddMemberRequest_And_Create_User);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1", FirstName = "Prof" };
        var admin = new User { Id = "admin1", FirstName = "Admin" };
        context.Users.AddRange(prof, admin);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var addMemberRequest = new AddMemberRequest
        {
            GroupId = group.Id,
            Email = "newstudent@example.com",
            RequestedByUserId = prof.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.AddMemberRequests.Add(addMemberRequest);
        await context.SaveChangesAsync();

        // Admin approves and creates user
        var request = await context.AddMemberRequests.FirstAsync();
        var newUser = new User
        {
            Id = "stu_new",
            FirstName = "New",
            LastName = "Student",
            Email = request.Email,
            StudentId = "AUTO_GEN_001",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        context.Users.Add(newUser);

        var newMember = new GroupMember
        {
            GroupId = group.Id,
            UserId = newUser.Id,
            Role = "Member",
            JoinedAt = DateTime.UtcNow,
            IsActive = true
        };

        context.GroupMembers.Add(newMember);

        request.Status = "Approved";
        request.ApprovedByUserId = admin.Id;
        request.ResolvedAt = DateTime.UtcNow;

        context.AddMemberRequests.Update(request);
        await context.SaveChangesAsync();

        var approvedRequest = await context.AddMemberRequests.FirstAsync();
        var createdUser = await context.Users.FirstAsync(u => u.Email == "newstudent@example.com");
        var groupMember = await context.GroupMembers.FirstAsync(gm => gm.UserId == createdUser.Id);

        Assert.Equal("Approved", approvedRequest.Status);
        Assert.Equal("newstudent@example.com", createdUser.Email);
        Assert.Equal("Member", groupMember.Role);
    }

    [Fact]
    public async System.Threading.Tasks.Task Admin_Can_Reject_AddMemberRequest()
    {
        var dbName = nameof(Admin_Can_Reject_AddMemberRequest);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1", FirstName = "Prof" };
        var admin = new User { Id = "admin1", FirstName = "Admin" };
        context.Users.AddRange(prof, admin);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var addMemberRequest = new AddMemberRequest
        {
            GroupId = group.Id,
            Email = "newstudent@example.com",
            RequestedByUserId = prof.Id,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };

        context.AddMemberRequests.Add(addMemberRequest);
        await context.SaveChangesAsync();

        // Admin rejects
        var request = await context.AddMemberRequests.FirstAsync();
        request.Status = "Rejected";
        request.ApprovedByUserId = admin.Id;
        context.AddMemberRequests.Update(request);
        await context.SaveChangesAsync();

        var rejectedRequest = await context.AddMemberRequests.FirstAsync();
        Assert.Equal("Rejected", rejectedRequest.Status);
    }

    [Fact]
    public async System.Threading.Tasks.Task Multiple_AddMemberRequests_For_Same_Group()
    {
        var dbName = nameof(Multiple_AddMemberRequests_For_Same_Group);
        using var context = CreateContext(dbName);

        var prof = new User { Id = "prof1" };
        context.Users.Add(prof);

        var group = new Group { Id = 1, Name = "G1", CreatedById = prof.Id, IsActive = true };
        group.Members.Add(new GroupMember { Group = group, UserId = prof.Id, Role = "Professor" });

        context.Groups.Add(group);
        context.GroupMembers.AddRange(group.Members);

        var req1 = new AddMemberRequest { GroupId = group.Id, Email = "user1@example.com", RequestedByUserId = prof.Id, Status = "Pending", CreatedAt = DateTime.UtcNow };
        var req2 = new AddMemberRequest { GroupId = group.Id, Email = "user2@example.com", RequestedByUserId = prof.Id, Status = "Pending", CreatedAt = DateTime.UtcNow };
        var req3 = new AddMemberRequest { GroupId = group.Id, Email = "user3@example.com", RequestedByUserId = prof.Id, Status = "Approved", ResolvedAt = DateTime.UtcNow };

        context.AddMemberRequests.AddRange(req1, req2, req3);
        await context.SaveChangesAsync();

        var pendingRequests = await context.AddMemberRequests
            .Where(amr => amr.GroupId == group.Id && amr.Status == "Pending")
            .ToListAsync();

        var allRequests = await context.AddMemberRequests
            .Where(amr => amr.GroupId == group.Id)
            .ToListAsync();

        Assert.Equal(2, pendingRequests.Count);
        Assert.Equal(3, allRequests.Count);
    }
}
