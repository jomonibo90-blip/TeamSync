using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Moq;
using TeamSync.Models;

namespace TeamSync.Tests.Helpers
{
    /// <summary>
    /// Helper class for testing SignalR hubs.
    /// Provides utilities for creating mocked SignalR components.
    /// </summary>
    public class SignalRTestHelper
    {
        /// <summary>
        /// Creates a mock HubCallerContext with the specified user ID.
        /// </summary>
        public static HubCallerContext CreateMockHubCallerContext(string userId)
        {
            var mock = new Mock<HubCallerContext>();
            var claims = new List<Claim> 
            { 
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, $"user-{userId}")
            };
            var identity = new ClaimsIdentity(claims);
            var principal = new ClaimsPrincipal(identity);

            mock.Setup(x => x.User).Returns(principal);
            mock.Setup(x => x.ConnectionId).Returns($"connection-{userId}");

            return mock.Object;
        }

        /// <summary>
        /// Creates a mock UserManager for testing.
        /// </summary>
        public static Mock<UserManager<User>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<User>>();
            var userManager = new Mock<UserManager<User>>(
                store.Object, null, null, null, null, null, null, null, null);
            return userManager;
        }

        /// <summary>
        /// Creates a mock HubCallerClients for capturing SendAsync calls.
        /// </summary>
        public static Mock<IHubCallerClients> CreateMockHubCallerClients()
        {
            var mock = new Mock<IHubCallerClients>();
            return mock;
        }

        /// <summary>
        /// Creates a mock IGroupManager for testing group operations.
        /// </summary>
        public static Mock<IGroupManager> CreateMockGroupManager()
        {
            var mock = new Mock<IGroupManager>();
            return mock;
        }

        /// <summary>
        /// Creates a mock ISingleClientProxy for capturing Caller SendAsync calls.
        /// </summary>
        public static Mock<ISingleClientProxy> CreateMockSingleClientProxy()
        {
            var mock = new Mock<ISingleClientProxy>();
            return mock;
        }

        /// <summary>
        /// Creates a mock IClientProxy for capturing broadcast SendAsync calls.
        /// </summary>
        public static Mock<IClientProxy> CreateMockClientProxy()
        {
            var mock = new Mock<IClientProxy>();
            return mock;
        }

        /// <summary>
        /// Sets up a user manager mock to return a specific user.
        /// </summary>
        public static void SetupUserGet(
            Mock<UserManager<User>> userManagerMock, 
            User user)
        {
            userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync(user);
        }

        /// <summary>
        /// Sets up a user manager mock to return null.
        /// </summary>
        public static void SetupUserGetNull(Mock<UserManager<User>> userManagerMock)
        {
            userManagerMock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>()))
                .ReturnsAsync((User)null);
        }

        /// <summary>
        /// Creates a test user with the specified ID.
        /// </summary>
        public static User CreateTestUser(string userId = "test-user-123")
        {
            return new User
            {
                Id = userId,
                UserName = $"user-{userId}",
                Email = $"user-{userId}@example.com"
            };
        }

        /// <summary>
        /// Creates a test notification with the specified parameters.
        /// </summary>
        public static Notification CreateTestNotification(
            int id = 1,
            string userId = "test-user-123",
            string type = "TestType",
            string message = "Test message",
            bool isRead = false,
            int? taskId = null)
        {
            return new Notification
            {
                Id = id,
                UserId = userId,
                Type = type,
                Message = message,
                IsRead = isRead,
                CreatedAt = DateTime.UtcNow,
                TaskId = taskId
            };
        }
    }
}
