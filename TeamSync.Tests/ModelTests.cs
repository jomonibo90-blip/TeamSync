using System;
using TeamSync.Models;
using Xunit;

namespace TeamSync.Tests;

public class UserModelTests
{
    [Fact]
    public void User_FullName_Property_Concatenates_FirstName_And_LastName()
    {
        var user = new User
        {
            FirstName = "John",
            LastName = "Doe",
            StudentId = "S001"
        };

        Assert.Equal("John Doe", user.FullName);
    }

    [Fact]
    public void User_StudentId_Is_Required()
    {
        var user = new User
        {
            FirstName = "Jane",
            LastName = "Smith"
        };

        Assert.NotNull(user.StudentId);
    }

    [Fact]
    public void User_IsActive_Defaults_To_True()
    {
        var user = new User { StudentId = "S001" };
        Assert.True(user.IsActive);
    }

    [Fact]
    public void User_CreatedAt_Defaults_To_UtcNow()
    {
        var user = new User { StudentId = "S001" };
        Assert.NotEqual(default(DateTime), user.CreatedAt);
    }
}

public class GroupModelTests
{
    [Fact]
    public void Group_Name_Is_Required()
    {
        var group = new Group();
        Assert.NotNull(group.Name);
    }

    [Fact]
    public void Group_IsArchived_Returns_True_When_InactiveOrArchived()
    {
        var group1 = new Group { IsActive = false };
        var group2 = new Group { IsActive = true, ArchivedAt = DateTime.UtcNow };
        var group3 = new Group { IsActive = true, ArchivedAt = null };

        Assert.True(group1.IsArchived);
        Assert.True(group2.IsArchived);
        Assert.False(group3.IsArchived);
    }

    [Fact]
    public void Group_CreatedAt_Defaults_To_UtcNow()
    {
        var group = new Group();
        Assert.NotEqual(default(DateTime), group.CreatedAt);
    }

    [Fact]
    public void Group_IsActive_Defaults_To_True()
    {
        var group = new Group();
        Assert.True(group.IsActive);
    }

    [Fact]
    public void Group_Members_Collection_Initialized()
    {
        var group = new Group();
        Assert.NotNull(group.Members);
        Assert.Empty(group.Members);
    }
}

public class TaskModelTests
{
    [Fact]
    public void Task_Title_Is_Required()
    {
        var task = new Task();
        Assert.NotNull(task.Title);
    }

    [Fact]
    public void Task_Status_Defaults_To_Pending()
    {
        var task = new Task();
        Assert.Equal("Pending", task.Status);
    }

    [Fact]
    public void Task_Priority_Defaults_To_Medium()
    {
        var task = new Task();
        Assert.Equal(2, task.Priority);
    }

    [Fact]
    public void Task_CreatedAt_Defaults_To_UtcNow()
    {
        var task = new Task();
        Assert.NotEqual(default(DateTime), task.CreatedAt);
    }
}

public class GroupMemberModelTests
{
    [Fact]
    public void GroupMember_IsActive_Defaults_To_True()
    {
        var member = new GroupMember();
        Assert.True(member.IsActive);
    }

    [Fact]
    public void GroupMember_JoinedAt_Defaults_To_UtcNow()
    {
        var member = new GroupMember();
        Assert.NotEqual(default(DateTime), member.JoinedAt);
    }

    [Fact]
    public void GroupMember_Role_Is_Set()
    {
        var member = new GroupMember { Role = "Professor" };
        Assert.Equal("Professor", member.Role);
    }
}

public class ContributionModelTests
{
    [Fact]
    public void Contribution_ContributedAt_Defaults_To_UtcNow()
    {
        var contribution = new Contribution();
        Assert.NotEqual(default(DateTime), contribution.ContributedAt);
    }

    [Fact]
    public void Contribution_HoursSpent_Is_Optional()
    {
        var contribution = new Contribution();
        Assert.Null(contribution.HoursSpent);
    }

    [Fact]
    public void Contribution_Source_Can_Be_Set()
    {
        var contribution = new Contribution { Source = "TaskFinalization" };
        Assert.Equal("TaskFinalization", contribution.Source);
    }

    [Fact]
    public void Contribution_With_HoursSpent()
    {
        var contribution = new Contribution { HoursSpent = 5.5m };
        Assert.Equal(5.5m, contribution.HoursSpent);
    }
}

public class RemovalRequestModelTests
{
    [Fact]
    public void RemovalRequest_Status_Defaults_To_Pending()
    {
        var request = new RemovalRequest();
        Assert.Equal("Pending", request.Status);
    }

    [Fact]
    public void RemovalRequest_CreatedAt_Defaults_To_UtcNow()
    {
        var request = new RemovalRequest();
        Assert.NotEqual(default(DateTime), request.CreatedAt);
    }

    [Fact]
    public void RemovalRequest_Can_Track_Approver()
    {
        var request = new RemovalRequest { ApprovedByUserId = "admin1" };
        Assert.Equal("admin1", request.ApprovedByUserId);
    }
}

public class JoinRequestModelTests
{
    [Fact]
    public void JoinRequest_Status_Defaults_To_Pending()
    {
        var request = new JoinRequest();
        Assert.Equal("Pending", request.Status);
    }

    [Fact]
    public void JoinRequest_CreatedAt_Defaults_To_UtcNow()
    {
        var request = new JoinRequest();
        Assert.NotEqual(default(DateTime), request.CreatedAt);
    }

    [Fact]
    public void JoinRequest_Can_Be_Approved()
    {
        var request = new JoinRequest { Status = "Approved", ApprovedByUserId = "prof1", ResolvedAt = DateTime.UtcNow };
        Assert.Equal("Approved", request.Status);
        Assert.NotNull(request.ResolvedAt);
    }

    [Fact]
    public void JoinRequest_Can_Be_Rejected()
    {
        var request = new JoinRequest { Status = "Rejected" };
        Assert.Equal("Rejected", request.Status);
    }
}
