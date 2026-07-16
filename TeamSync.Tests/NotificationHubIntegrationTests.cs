using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using TeamSync.Data;
using TeamSync.Hubs;
using TeamSync.Models;
using TeamSync.Tests.Helpers;
using Task = System.Threading.Tasks.Task;

namespace TeamSync.Tests
{
    /// <summary>
    /// Integration tests for NotificationHub.
    /// Tests complete hub workflows with real database context and multiple user scenarios.
    /// </summary>
    public class NotificationHubIntegrationTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CompleteNotificationWorkflow_UserConnectsAndReceivesNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            // Create some notifications for the user
            var notifications = new List<Notification>
            {
                SignalRTestHelper.CreateTestNotification(1, userId, "TaskAssignment", "Task assigned"),
                SignalRTestHelper.CreateTestNotification(2, userId, "StatusChange", "Task completed"),
                SignalRTestHelper.CreateTestNotification(3, userId, "Comment", "New comment", false, null)
            };

            context.Notifications.AddRange(notifications);
            await context.SaveChangesAsync();

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act - Connect user
            await hub.OnConnectedAsync();

            // Assert - Verify user was added to group
            groupsManagerMock.Verify(
                x => x.AddToGroupAsync(
                    It.IsAny<string>(),
                    $"user-{userId}",
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);

            // Act - Get unread count
            await hub.GetUnreadCount();

            // Assert - Verify unread count is correct
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "UnreadCountUpdated",
                    It.Is<object[]>(o => (int)o[0] == 3),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);

            // Act - Get recent notifications
            await hub.GetRecentNotifications();

            // Assert - Verify notifications were sent
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.IsAny<object[]>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task MultipleUsers_ReceiveOnlyTheirOwnNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId1 = "user-1";
            var userId2 = "user-2";
            var user1 = SignalRTestHelper.CreateTestUser(userId1);
            var user2 = SignalRTestHelper.CreateTestUser(userId2);

            // Add notifications for both users
            context.Notifications.AddRange(
                SignalRTestHelper.CreateTestNotification(1, userId1, "Type1", "msg1"),
                SignalRTestHelper.CreateTestNotification(2, userId1, "Type2", "msg2"),
                SignalRTestHelper.CreateTestNotification(3, userId2, "Type3", "msg3"),
                SignalRTestHelper.CreateTestNotification(4, userId2, "Type4", "msg4")
            );
            await context.SaveChangesAsync();

            // Setup user1 hub
            userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(user1);

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub1 = new NotificationHub(userManager.Object, context);
            hub1.Clients = clientsProxyMock.Object;
            hub1.Groups = groupsManagerMock.Object;
            hub1.Context = SignalRTestHelper.CreateMockHubCallerContext(userId1);

            // Act - User1 gets recent notifications
            await hub1.GetRecentNotifications();

            // Assert - User1 should only see their own notifications
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.IsAny<object[]>(),
                    It.IsAny<System.Threading.CancellationToken>()));
        }

        [Fact]
        public async Task MarkAsRead_UpdatesOnlySpecificNotification()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            var notif1 = SignalRTestHelper.CreateTestNotification(1, userId, "Type1", "msg1", false);
            var notif2 = SignalRTestHelper.CreateTestNotification(2, userId, "Type2", "msg2", false);

            context.Notifications.AddRange(notif1, notif2);
            await context.SaveChangesAsync();

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act - Mark first notification as read
            await hub.MarkAsRead(1);

            // Assert - First should be read, second should still be unread
            var notif1Updated = await context.Notifications.FindAsync(1);
            var notif2Updated = await context.Notifications.FindAsync(2);

            Assert.NotNull(notif1Updated);
            Assert.NotNull(notif2Updated);
            Assert.True(notif1Updated.IsRead);
            Assert.False(notif2Updated.IsRead);

            // Act - Get unread count
            await hub.GetUnreadCount();

            // Assert - Should show 1 unread notification
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "UnreadCountUpdated",
                    It.Is<object[]>(o => (int)o[0] == 1),
                    It.IsAny<System.Threading.CancellationToken>()));
        }

        [Fact]
        public async Task GetRecentNotifications_RespectsPaginationLimit()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            // Create 20 notifications
            var notifications = Enumerable.Range(1, 20)
                .Select(i => SignalRTestHelper.CreateTestNotification(
                    i, userId, $"Type{i}", $"msg{i}", false, null))
                .ToList();

            context.Notifications.AddRange(notifications);
            await context.SaveChangesAsync();

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act - Request with specific limit
            await hub.GetRecentNotifications(5);

            // Assert - Should send exactly 5 notifications
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.Is<object[]>(o => ((List<dynamic>)o[0]).Count == 5),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task GetRecentNotifications_IncludesTaskDetails()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            // Create a task
            var task = new Models.Task
            {
                Id = 1,
                Title = "Important Task",
                Description = "Do something important",
                Status = "InProgress",
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            };

            context.Tasks.Add(task);

            // Create notification linked to task
            var notification = new Notification
            {
                UserId = userId,
                Type = "TaskStatusChange",
                Message = "Task status changed",
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                TaskId = 1,
                Task = task
            };

            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications();

            // Assert - Should include task title
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.IsAny<object[]>(),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task UnauthorizedUser_CannotAccessOtherUsersNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId1 = "user-1";
            var userId2 = "user-2";
            var user1 = SignalRTestHelper.CreateTestUser(userId1);
            var user2 = SignalRTestHelper.CreateTestUser(userId2);

            // Create notifications for user2
            var notification = SignalRTestHelper.CreateTestNotification(1, userId2, "Type1", "msg1");
            context.Notifications.Add(notification);
            await context.SaveChangesAsync();

            // Setup hub as user1
            userManager.Setup(x => x.GetUserAsync(It.IsAny<System.Security.Claims.ClaimsPrincipal>()))
                .ReturnsAsync(user1);

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId1);

            // Act - Try to mark user2's notification as read
            await hub.MarkAsRead(1);

            // Assert - Notification should remain unread
            var notification2 = await context.Notifications.FindAsync(1);
            Assert.NotNull(notification2);
            Assert.False(notification2.IsRead);

            // No client message should be sent
            callerProxyMock.Verify(
                x => x.SendCoreAsync(It.IsAny<string>(), It.IsAny<object[]>(), It.IsAny<System.Threading.CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task NoNotifications_GetRecentNotificationsReturnsEmptyList()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            // No notifications added to context

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act
            await hub.GetRecentNotifications();

            // Assert
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "LoadRecentNotifications",
                    It.Is<object[]>(o => ((List<dynamic>)o[0]).Count == 0),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task MixedReadAndUnreadNotifications_GetUnreadCountIsAccurate()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userManager = SignalRTestHelper.CreateMockUserManager();

            var userId = "test-user-123";
            var user = SignalRTestHelper.CreateTestUser(userId);

            SignalRTestHelper.SetupUserGet(userManager, user);

            // Create mix of read and unread notifications
            context.Notifications.AddRange(
                SignalRTestHelper.CreateTestNotification(1, userId, "Type1", "msg1", false),
                SignalRTestHelper.CreateTestNotification(2, userId, "Type2", "msg2", false),
                SignalRTestHelper.CreateTestNotification(3, userId, "Type3", "msg3", true),
                SignalRTestHelper.CreateTestNotification(4, userId, "Type4", "msg4", false),
                SignalRTestHelper.CreateTestNotification(5, userId, "Type5", "msg5", true)
            );
            await context.SaveChangesAsync();

            var groupsManagerMock = SignalRTestHelper.CreateMockGroupManager();
            var clientsProxyMock = SignalRTestHelper.CreateMockHubCallerClients();
            var callerProxyMock = SignalRTestHelper.CreateMockSingleClientProxy();

            clientsProxyMock.Setup(x => x.Caller).Returns(callerProxyMock.Object);

            var hub = new NotificationHub(userManager.Object, context);
            hub.Clients = clientsProxyMock.Object;
            hub.Groups = groupsManagerMock.Object;
            hub.Context = SignalRTestHelper.CreateMockHubCallerContext(userId);

            // Act
            await hub.GetUnreadCount();

            // Assert - Should count exactly 3 unread
            callerProxyMock.Verify(
                x => x.SendCoreAsync(
                    "UnreadCountUpdated",
                    It.Is<object[]>(o => (int)o[0] == 3),
                    It.IsAny<System.Threading.CancellationToken>()),
                Times.Once);
        }
    }
}
