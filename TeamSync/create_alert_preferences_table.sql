-- Create AlertPreferences table
CREATE TABLE [AlertPreferences] (
	[Id] int NOT NULL IDENTITY(1, 1),
	[UserId] nvarchar(450) NOT NULL,
	[NotificationFrequency] nvarchar(50) NOT NULL DEFAULT 'Weekly',
	[DigestDayOfWeek] int DEFAULT 1,
	[DigestHourUtc] int DEFAULT 9,
	[ReceiveTaskAssignmentAlerts] bit NOT NULL DEFAULT 1,
	[ReceiveApprovalRejectionAlerts] bit NOT NULL DEFAULT 1,
	[ReceiveStatusChangeAlerts] bit NOT NULL DEFAULT 1,
	[ReceiveCommentAlerts] bit NOT NULL DEFAULT 1,
	[ReceiveGroupAlerts] bit NOT NULL DEFAULT 1,
	[CreatedAt] datetime2 NOT NULL DEFAULT GETUTCDATE(),
	[UpdatedAt] datetime2,
	[LastDigestSentAt] datetime2,
	CONSTRAINT [PK_AlertPreferences] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_AlertPreferences_AspNetUsers_UserId] FOREIGN KEY ([UserId])
		REFERENCES [AspNetUsers]([Id]) ON DELETE CASCADE
);

-- Create unique index on UserId
CREATE UNIQUE INDEX [IX_AlertPreferences_UserId] ON [AlertPreferences]([UserId]);
