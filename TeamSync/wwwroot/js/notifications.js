/**
 * SignalR Notification Client
 * Handles real-time notification connection, receiving notifications,
 * and updating the UI in real-time.
 */

class NotificationClient {
    constructor() {
        this.connection = null;
        this.unreadCount = 0;
        this.notifications = [];
        this.maxNotificationsDisplay = 10;
    }

    /**
     * Initialize the SignalR connection and set up event handlers.
     */
    async init() {
        // Wait for SignalR to be available
        if (typeof signalR === 'undefined') {
            console.warn('SignalR not available for notifications');
            return;
        }

        // Build the SignalR connection
        this.connection = new signalR.HubConnectionBuilder()
            .withUrl("/notificationHub")
            .withAutomaticReconnect([0, 0, 3000, 5000, 10000, 30000])
            .build();

        // Set up event handlers
        this.setupHandlers();

        try {
            await this.connection.start();
            console.log("SignalR connection established.");

            // Request initial data after connection
            await this.requestUnreadCount();
            await this.requestRecentNotifications();
        } catch (error) {
            console.error("Failed to start SignalR connection:", error);
            // Retry after 5 seconds
            setTimeout(() => this.init(), 5000);
        }
    }

    /**
     * Set up all SignalR hub event handlers.
     */
    setupHandlers() {
        // New notification received
        this.connection.on("NewNotification", (notification) => {
            console.log("New notification received:", notification);
            this.addNotificationToUI(notification);
            this.updateUnreadBadge();
        });

        // Unread count updated
        this.connection.on("UnreadCountUpdated", (count) => {
            console.log("Unread count updated:", count);
            this.unreadCount = count;
            this.updateUnreadBadge();
        });

        // Notification marked as read
        this.connection.on("NotificationMarkedAsRead", (notificationId) => {
            console.log("Notification marked as read:", notificationId);
            this.unreadCount = Math.max(0, this.unreadCount - 1);
            this.updateUnreadBadge();
            this.removeNotificationFromUI(notificationId);
        });

        // All notifications marked as read
        this.connection.on("AllNotificationsMarkedAsRead", (notificationIds) => {
            console.log("All notifications marked as read:", notificationIds);
            this.unreadCount = 0;
            this.updateUnreadBadge();
            this.clearNotificationsUI();
        });

        // Recent notifications loaded
        this.connection.on("LoadRecentNotifications", (notifications) => {
            console.log("Recent notifications loaded:", notifications);
            this.notifications = notifications;
            this.refreshNotificationsUI();
        });

        // Connection reconnected
        this.connection.onreconnected(() => {
            console.log("Reconnected to SignalR hub.");
            this.requestUnreadCount();
            this.requestRecentNotifications();
        });

        // Connection disconnected
        this.connection.onclose(() => {
            console.log("Disconnected from SignalR hub.");
        });
    }

    /**
     * Add a new notification to the UI dropdown.
     */
    addNotificationToUI(notification) {
        // Add to the beginning of the array
        this.notifications.unshift(notification);

        // Keep only the most recent notifications
        if (this.notifications.length > this.maxNotificationsDisplay) {
            this.notifications.pop();
        }

        this.refreshNotificationsUI();
    }

    /**
     * Remove a notification from the UI.
     */
    removeNotificationFromUI(notificationId) {
        this.notifications = this.notifications.filter(n => n.id !== notificationId);
        this.refreshNotificationsUI();
    }

    /**
     * Clear all notifications from the UI.
     */
    clearNotificationsUI() {
        this.notifications = [];
        this.refreshNotificationsUI();
    }

    /**
     * Refresh the notifications dropdown HTML.
     */
    refreshNotificationsUI() {
        const dropdown = document.getElementById("notificationDropdown");
        if (!dropdown) return;

        if (this.notifications.length === 0) {
            dropdown.innerHTML = `<div class="notification-empty">No notifications</div>`;
            return;
        }

        const html = this.notifications.map(n => `
            <div class="notification-item ${n.isRead ? 'read' : 'unread'}">
                <div class="notification-header">
                    <span class="notification-type">${this.formatType(n.type)}</span>
                    <button class="btn-close-notification" onclick="notificationClient.markAsRead(${n.id})" title="Mark as read">×</button>
                </div>
                <div class="notification-message">${this.escapeHtml(n.message)}</div>
                <div class="notification-time">${this.formatTime(n.createdAt)}</div>
                ${n.taskId ? `<a href="/Tasks/Details/${n.taskId}" class="notification-task-link">View Task</a>` : ''}
            </div>
        `).join('');

        dropdown.innerHTML = html;

        // Also update dashboard panel if it exists
        this.updateDashboardPanel();
    }

    /**
     * Update the dashboard notifications panel.
     */
    updateDashboardPanel() {
        const panel = document.getElementById("dashboardNotificationsPanel");
        const body = document.getElementById("dashboardNotificationsBody");

        if (!panel || !body) return;

        if (this.unreadCount === 0) {
            panel.style.display = 'none';
            return;
        }

        panel.style.display = 'block';

        const unreadNotifications = this.notifications.filter(n => !n.isRead).slice(0, 5);

        if (unreadNotifications.length === 0) {
            body.innerHTML = '<div class="notification-empty">All notifications read</div>';
            return;
        }

        const html = unreadNotifications.map(n => {
            const icon = n.type === 'DeadlineReminder' ? '⏰' : 
                        n.type === 'StatusChange' ? '📝' : 
                        '📬';
            const typeClass = n.type === 'DeadlineReminder' ? 'deadline' : 
                             n.type === 'StatusChange' ? 'status-change' : '';

            return `
                <div class="ts-dashboard-notification-item ${typeClass}">
                    <div class="ts-notification-icon">${icon}</div>
                    <div class="ts-notification-content">
                        <div class="ts-notification-content-title">${this.escapeHtml(n.message.substring(0, 50))}${n.message.length > 50 ? '...' : ''}</div>
                        <div class="ts-notification-content-message">${this.formatTime(n.createdAt)}</div>
                    </div>
                    <button class="ts-notification-close" onclick="notificationClient.markAsRead(${n.id})" title="Dismiss" aria-label="Dismiss notification">×</button>
                </div>
            `;
        }).join('');

        body.innerHTML = html;
    }

    /**
     * Update the unread notification badge.
     */
    updateUnreadBadge() {
        const badge = document.getElementById("notificationBadge");
        if (!badge) return;

        if (this.unreadCount > 0) {
            badge.textContent = this.unreadCount > 99 ? '99+' : this.unreadCount;
            badge.style.display = 'inline-block';
        } else {
            badge.style.display = 'none';
        }
    }

    /**
     * Request unread count from the hub.
     */
    async requestUnreadCount() {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            try {
                const count = await this.connection.invoke("GetUnreadCount");
                this.unreadCount = count;
                this.updateUnreadBadge();
            } catch (error) {
                console.error("Error requesting unread count:", error);
            }
        }
    }

    /**
     * Request recent notifications from the hub.
     */
    async requestRecentNotifications(limit = 10) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            try {
                const notifications = await this.connection.invoke("GetRecentNotifications", limit);
                this.notifications = notifications;
                this.refreshNotificationsUI();
            } catch (error) {
                console.error("Error requesting recent notifications:", error);
            }
        }
    }

    /**
     * Mark a notification as read via hub.
     */
    async markAsRead(notificationId) {
        if (this.connection && this.connection.state === signalR.HubConnectionState.Connected) {
            try {
                await this.connection.invoke("MarkAsRead", notificationId);
            } catch (error) {
                console.error("Error marking notification as read:", error);
            }
        }
    }

    /**
     * Format notification type for display.
     */
    formatType(type) {
        return type === 'DeadlineReminder' ? '⏰ Deadline' : 
               type === 'StatusChange' ? '📝 Status' : 
               type;
    }

    /**
     * Format timestamp for display.
     */
    formatTime(isoString) {
        const date = new Date(isoString);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'Just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;

        return date.toLocaleDateString();
    }

    /**
     * Escape HTML to prevent XSS.
     */
    escapeHtml(text) {
        const map = {
            '&': '&amp;',
            '<': '&lt;',
            '>': '&gt;',
            '"': '&quot;',
            "'": '&#039;'
        };
        return text.replace(/[&<>"']/g, m => map[m]);
    }
}

// Initialize the notification client when page loads
let notificationClient;
document.addEventListener("DOMContentLoaded", function () {
    // Only initialize if user is authenticated (will fail otherwise)
    if (document.body.classList.contains('authenticated')) {
        notificationClient = new NotificationClient();
        notificationClient.init();
    }
});

/**
 * Toggle notification dropdown visibility
 */
function toggleNotificationDropdown() {
    const dropdown = document.getElementById("notificationDropdown");
    if (dropdown) {
        dropdown.classList.toggle("show");
    }
}

/**
 * Close notification dropdown when clicking outside
 */
document.addEventListener("click", function (event) {
    const dropdown = document.getElementById("notificationDropdown");
    const btn = document.getElementById("notificationBtn");

    if (dropdown && btn && !dropdown.contains(event.target) && !btn.contains(event.target)) {
        dropdown.classList.remove("show");
    }
});
