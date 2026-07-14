using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TeamSync.Controllers;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.ViewModels;
using Xunit;

using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests;

public class HomeControllerTests
{
    private ApplicationDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private class DummyUserStore : IUserStore<User>
    {
        public System.Threading.Tasks.Task<IdentityResult> CreateAsync(User user, System.Threading.CancellationToken cancellationToken) => throw new NotImplementedException();
        public System.Threading.Tasks.Task<IdentityResult> DeleteAsync(User user, System.Threading.CancellationToken cancellationToken) => throw new NotImplementedException();
        public void Dispose() { }
        public System.Threading.Tasks.Task<User?> FindByIdAsync(string userId, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult<User?>(null);
        public System.Threading.Tasks.Task<User?> FindByNameAsync(string normalizedUserName, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult<User?>(null);
        public System.Threading.Tasks.Task<string?> GetNormalizedUserNameAsync(User user, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult<string?>(null);
        public System.Threading.Tasks.Task<string> GetUserIdAsync(User user, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult(user?.Id ?? "");
        public System.Threading.Tasks.Task<string?> GetUserNameAsync(User user, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.FromResult<string?>(user?.UserName);
        public System.Threading.Tasks.Task SetNormalizedUserNameAsync(User user, string? normalizedName, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task SetUserNameAsync(User user, string? userName, System.Threading.CancellationToken cancellationToken) => System.Threading.Tasks.Task.CompletedTask;
        public System.Threading.Tasks.Task<IdentityResult> UpdateAsync(User user, System.Threading.CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private class FakeUserManager : UserManager<User>
    {
        private readonly User _userToReturn;
        private readonly Func<User, string, System.Threading.Tasks.Task<bool>> _isInRoleFunc;

        public FakeUserManager(User userToReturn, Func<User, string, System.Threading.Tasks.Task<bool>>? isInRoleFunc = null)
            : base(new DummyUserStore(), null!, null!, null!, null!, null!, null!, null!, null)
        {
            _userToReturn = userToReturn;
            _isInRoleFunc = isInRoleFunc ?? ((u, r) => System.Threading.Tasks.Task.FromResult(false));
        }

        public override System.Threading.Tasks.Task<User?> GetUserAsync(ClaimsPrincipal principal)
        {
            return System.Threading.Tasks.Task.FromResult<User?>(_userToReturn);
        }

        public override System.Threading.Tasks.Task<bool> IsInRoleAsync(User user, string role)
        {
            return _isInRoleFunc(user, role);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task Student_Dashboard_Aggregates_Contributions_And_Tasks()
    {
        var dbName = nameof(Student_Dashboard_Aggregates_Contributions_And_Tasks);
        using var context = CreateContext(dbName);

        var student = new User { Id = "stu1", FirstName = "Stu", StudentId = "S001" };
        context.Users.Add(student);

        // group and membership
        var group = new Group { Id = 10, Name = "Proj10", IsActive = true };
        var gm = new GroupMember { Group = group, UserId = student.Id, Role = "Member" };
        group.Members.Add(gm);
        context.Groups.Add(group);
        context.GroupMembers.Add(gm);

        // tasks assigned
        var t1 = new ModelTask { Title = "Task1", Group = group, AssignedToId = student.Id, Status = "Completed", CreatedAt = DateTime.UtcNow };
        var t2 = new ModelTask { Title = "Task2", Group = group, AssignedToId = student.Id, Status = "InProgress", CreatedAt = DateTime.UtcNow };
        context.Tasks.AddRange(t1, t2);

        // contribution for t1
        var contrib = new Contribution { Task = t1, UserId = student.Id, Description = "Completed task", ContributedAt = DateTime.UtcNow, HoursSpent = 3.5m };
        context.Contributions.Add(contrib);

        await context.SaveChangesAsync();

        var userManager = new FakeUserManager(student, (u, r) => System.Threading.Tasks.Task.FromResult(false));

        var controller = new HomeController(context, userManager);
        var claims = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, student.Id) }, "TestAuth"));
        controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = claims } };

        var result = await controller.Dashboard();
        var viewResult = Assert.IsType<ViewResult>(result);
        Assert.Equal("StudentDashboard", viewResult.ViewName);

        var model = Assert.IsType<StudentDashboardViewModel>(viewResult.Model!);
        Assert.Equal(2, model.Progress.TotalTasks);
        Assert.Equal(1, model.Progress.CompletedTasks);
        Assert.Equal(1, model.Progress.InProgressTasks);
        Assert.Equal(3.5m, model.Progress.TotalHoursContributed);
        Assert.Equal(1, model.Progress.ContributionsCount);
    }
}
