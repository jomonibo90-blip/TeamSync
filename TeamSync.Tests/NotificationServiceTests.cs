using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using TeamSync.Data;
using TeamSync.Models;
using TeamSync.Services;
using Task = System.Threading.Tasks.Task;
using ModelTask = TeamSync.Models.Task;

namespace TeamSync.Tests
{
    /// <summary>
    /// Unit tests for NotificationService logic.
    /// Tests notification creation, persistence, deduplication, and retrieval.
    /// </summary>
    public class NotificationServiceTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task CreateNotificationAsync_CreatesAndPersistsNotification()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!); // null HubContext for unit test

            var userId = "test-user-123";
            var taskId = 1;
            var notificationType = "TaskAssignment";
            var message = "You have been assigned a new task";

            // Act
            await service.CreateNotificationAsync(
                userId, 
                notificationType, 
                message, 
                taskId
            );

            // Assert
            var createdNotification = await context.Notifications
                .Where(n => n.UserId == userId && n.Type == notificationType)
                .FirstOrDefaultAsync();

            Assert.NotNull(createdNotification);
            Assert.Equal(userId, createdNotification.UserId);
            Assert.Equal(taskId, createdNotification.TaskId);
            Assert.Equal(notificationType, createdNotification.Type);
            Assert.Equal(message, createdNotification.Message);
            Assert.False(createdNotification.IsRead);
        }

        [Fact]
        public async Task CreateNotificationsForUsersAsync_CreatesMultipleNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            var userIds = new List<string> { "user-1", "user-2", "user-3" };
            var notificationType = "StatusChange";
            var message = "Task status updated";
            var taskId = 42;

            // Act
            await service.CreateNotificationsForUsersAsync(
                userIds,
                notificationType,
                message,
                taskId
            );

            // Assert
            var notifications = await context.Notifications
                .Where(n => n.Type == notificationType && n.TaskId == taskId)
                .ToListAsync();

            Assert.Equal(3, notifications.Count);
            Assert.All(notifications, n => 
            {
                Assert.Equal(notificationType, n.Type);
                Assert.Equal(message, n.Message);
                Assert.Equal(taskId, n.TaskId);
                Assert.False(n.IsRead);
            });
        }

        [Fact]
        public async Task GetUnreadCountAsync_ReturnsCorrectCount()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            var userId = "test-user-456";

            // Create 3 unread and 2 read notifications
            var unreadNotifications = new List<Notification>
            {
                new Notification { UserId = userId, Type = "Test1", Message = "msg1", IsRead = false, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = userId, Type = "Test2", Message = "msg2", IsRead = false, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = userId, Type = "Test3", Message = "msg3", IsRead = false, CreatedAt = DateTime.UtcNow }
            };

            var readNotifications = new List<Notification>
            {
                new Notification { UserId = userId, Type = "Test4", Message = "msg4", IsRead = true, CreatedAt = DateTime.UtcNow },
                new Notification { UserId = userId, Type = "Test5", Message = "msg5", IsRead = true, CreatedAt = DateTime.UtcNow }
            };

            await context.Notifications.AddRangeAsync(unreadNotifications);
            await context.Notifications.AddRangeAsync(readNotifications);
            await context.SaveChangesAsync();

            // Act
            var unreadCount = await service.GetUnreadCountAsync(userId);

            // Assert
            Assert.Equal(3, unreadCount);
        }

        [Fact]
        public async Task MarkAsReadAsync_UpdatesNotificationStatus()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            var userId = "test-user-789";
            var notification = new Notification
            {
                UserId = userId,
                Type = "TestNotification",
                Message = "test message",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await context.Notifications.AddAsync(notification);
            await context.SaveChangesAsync();
            var notificationId = notification.Id;

            // Act
            await service.MarkAsReadAsync(notificationId);

            // Assert
            var updatedNotification = await context.Notifications.FindAsync(notificationId);
            Assert.NotNull(updatedNotification);
            Assert.True(updatedNotification.IsRead);
        }

        [Fact]
        public async Task HasRecentNotificationAsync_DetectsRecentDuplicates()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            var userId = "dedup-test-user";
            var taskId = 99;
            var notificationType = "Duplicate";
            var minutesThreshold = 5;

            var recentNotification = new Notification
            {
                UserId = userId,
                TaskId = taskId,
                Type = notificationType,
                Message = "Recent notification",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-2) // 2 minutes ago
            };

            await context.Notifications.AddAsync(recentNotification);
            await context.SaveChangesAsync();

            // Act
            var hasDuplicate = await service.HasRecentNotificationAsync(
                userId,
                notificationType,
                taskId,
                minutesThreshold
            );

            // Assert
            Assert.True(hasDuplicate);
        }

        [Fact]
        public async Task HasRecentNotificationAsync_IgnoresOldNotifications()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            var userId = "old-notif-user";
            var taskId = 100;
            var notificationType = "OldNotif";
            var minutesThreshold = 5;

            var oldNotification = new Notification
            {
                UserId = userId,
                TaskId = taskId,
                Type = notificationType,
                Message = "Old notification",
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddMinutes(-15) // 15 minutes ago
            };

            await context.Notifications.AddAsync(oldNotification);
            await context.SaveChangesAsync();

            // Act
            var hasDuplicate = await service.HasRecentNotificationAsync(
                userId,
                notificationType,
                taskId,
                minutesThreshold
            );

            // Assert
            Assert.False(hasDuplicate);
        }

        [Fact]
        public async Task NotificationPersistence_ExcludesUserIdFromRequiredCheck()
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var userId = "required-user-123";

            var notification = new Notification
            {
                UserId = userId,
                Type = "RequiredFieldTest",
                Message = "Test message with required fields",
                IsRead = false,
                TaskId = null, // Optional
                CreatedAt = DateTime.UtcNow
            };

            // Act & Assert - Should persist without exception
            await context.Notifications.AddAsync(notification);
            await context.SaveChangesAsync();

            var persisted = await context.Notifications
                .FirstOrDefaultAsync(n => n.UserId == userId);
            Assert.NotNull(persisted);
        }

        [Theory]
        [InlineData("Assignment")]
        [InlineData("StatusChange")]
        [InlineData("ApprovalRequired")]
        [InlineData("DeadlineReminder")]
        public async Task CreateNotificationAsync_SupportsMultipleNotificationTypes(string notificationType)
        {
            // Arrange
            var context = GetInMemoryDbContext();
            var service = new NotificationService(context, null!);

            // Act
            await service.CreateNotificationAsync(
                "test-user",
                notificationType,
                $"Message for {notificationType}",
                taskId: 1
            );

            // Assert
            var persisted = await context.Notifications
                .Where(n => n.UserId == "test-user" && n.Type == notificationType)
                .FirstOrDefaultAsync();

            Assert.NotNull(persisted);
            Assert.Equal(notificationType, persisted.Type);
        }
    }
}
