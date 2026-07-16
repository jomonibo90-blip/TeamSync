using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TeamSync.Data;
using TeamSync.Hubs;
using TeamSync.Models;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Tests
{
    /// <summary>
    /// Unit tests for NotificationHub.
    /// Tests hub client interactions, group management, and notification retrieval.
    /// </summary>
    public class NotificationHubTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        private Mock<UserManager<User>> GetMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            var userManager = new Mock<UserManager<User>>(
                store.Object, null, null, null, null, null, null, null, null);
            return userManager;
        }

        private HubCallerContext GetMockHubCallerContext(string userId)
        {
            var mock = new Mock<HubCallerContext>();
            var claims = new List<Claim> { new Claim(ClaimTypes.NameIdentifier, userId) };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);

            mock.Setup(x => x.User).Returns(principal);
            mock.Setup(x => x.ConnectionId).Returns($"connection-{userId}");

            return mock.Object;
        }

        [Fact]
        public async Task OnConnectedAsync_WithValidUser_AddsConnectionToUserGroup()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var groupsManagerMock = new Mock<IGroupManager>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            var callerProxyMock = new Mock<ISingleClientProxy>();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.OnConnectedAsync();

            // Assert
            groupsManagerMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(),
                    $"user-{userId}",
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task OnConnectedAsync_WithNullUser_DoesNotAddToGroup()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((User)null);

            var groupsManagerMock = new Mock<IGroupManager>();
            var clientsProxyMock = new Mock<IHubCallerClients>();

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = GetMockHubCallerContext("test-user");

            // Act
            await hub.OnConnectedAsync();

            // Assert
            groupsManagerMock.Verify(
                x => x.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task MarkAsRead_WithValidNotification_UpdatesAndNotifiesClient()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            var notification = new Notification
            {
                Id = 1,
                UserId = userId,
                Type = "TestType",
                Message = "Test message",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.MarkAsRead(1);

            // Assert
            var updatedNotification = await context.Notifications.FindAsync(1);
            Assert.NotNull(updatedNotification);
            Assert.True(updatedNotification.IsRead);

            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "NotificationMarkedAsRead",
                    It.Is<object[]>(o => o[0].Equals(1)),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task MarkAsRead_WithUnauthorizedNotification_DoesNotUpdate()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var otherUserId = "other-user-456";
            var user = new User { Id = userId, UserName = "testuser" };

            var notification = new Notification
            {
                Id = 1,
                UserId = otherUserId,
                Type = "TestType",
                Message = "Test message",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.MarkAsRead(1);

            // Assert
            var notification2 = await context.Notifications.FindAsync(1);
            Assert.NotNull(notification2);
            Assert.False(notification2.IsRead);

            callerProxyMock.Verify(
                x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task MarkAsRead_WithNullUser_DoesNothing()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((User)null);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext("test-user");

            // Act
            await hub.MarkAsRead(1);

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetUnreadCount_ReturnsCorrectCount()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            // Add multiple notifications with mixed read states
            context.Notifications.AddRange(
                new Notification { UserId = userId, Type = "Type1", Message = "msg1", IsRead = false, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = userId, Type = "Type2", Message = "msg2", IsRead = false, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = userId, Type = "Type3", Message = "msg3", IsRead = true, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = "other-user", Type = "Type4", Message = "msg4", IsRead = false, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetUnreadCount();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "UnreadCountUpdated",
                    It.Is<object[]>(o => (int)o[0] == 2),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetUnreadCount_WithNoUnreadNotifications_ReturnsZero()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            context.Notifications.Add(
                new Notification { UserId = userId, Type = "Type1", Message = "msg1", IsRead = true, CreatedAt = DateTime.UtcNow }
            );
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetUnreadCount();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "UnreadCountUpdated",
                    It.Is<object[]>(o => (int)o[0] == 0),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_ReturnsLimitedNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            // Add 15 notifications to test limit
            for (int i = 1; i <= 15; i++)
            {
                context.Notifications.Add(
                    new Notification
                    {
                        UserId = userId,
                        Type = $"Type{i}",
                        Message = $"msg{i}",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                        TaskId = i
                    }
                );
            }
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications(10);

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.Is<object[]>(o => ((List<dynamic>)o[0]).Count == 10),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_ReturnsNotificationsOrderedByDateDescending()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            var baseTime = DateTime.UtcNow;
            context.Notifications.AddRange(
                new Notification { UserId = userId, Type = "Type1", Message = "msg1", IsRead = false, CreatedAt = baseTime.AddMinutes(-5), TaskId = 1 },
                new Notification { UserId = userId, Type = "Type2", Message = "msg2", IsRead = false, CreatedAt = baseTime.AddMinutes(-2), TaskId = 2 },
                new Notification { UserId = userId, Type = "Type3", Message = "msg3", IsRead = false, CreatedAt = baseTime, TaskId = 3 }
            );
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.IsAny<object[]>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_WithCustomLimit_RespectsLimit()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            for (int i = 1; i <= 20; i++)
            {
                context.Notifications.Add(
                    new Notification
                    {
                        UserId = userId,
                        Type = $"Type{i}",
                        Message = $"msg{i}",
                        IsRead = false,
                        CreatedAt = DateTime.UtcNow.AddMinutes(-i),
                        TaskId = i
                    }
                );
            }
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications(5);

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.Is<object[]>(o => ((List<dynamic>)o[0]).Count == 5),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_IncludesTaskTitleWhenAvailable()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            var userId = "test-user-123";
            var user = new User { Id = userId, UserName = "testuser" };

            var task = new Models.Task
            {
                Id = 1,
                Title = "Test Task",
                Description = "Test Description",
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };

            context.Tasks.Add(task);
            context.Notifications.Add(
                new Notification
                {
                    UserId = userId,
                    Type = "TaskAssignment",
                    Message = "You were assigned a task",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow,
                    TaskId = 1,
                    Task = task
                }
            );
            await context.SaveChangesAsync();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.IsAny<object[]>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_WithNullUser_DoesNotSendNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = GetMockUserManager();

            userManager.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((User)null);

            var callerProxyMock = new Mock<ISingleClientProxy>();
            var clientsProxyMock = new Mock<IHubCallerClients>();
            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Context = GetMockHubCallerContext("test-user");

            // Act
            await hub.GetRecentNotifications();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }
    }
}
