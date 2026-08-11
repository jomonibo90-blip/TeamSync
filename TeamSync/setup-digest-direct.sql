-- Setup AlertPreferences for weekly digest emails
-- This script creates alert preferences for a demo user to receive weekly digests

-- Step 1: Find users and count their contributions
SELECT TOP 5 
	u.Id,
	u.Email,
	u.FirstName,
	u.LastName,
	(SELECT COUNT(*) FROM Contributions WHERE UserId = u.Id) AS ContributionCount
FROM AspNetUsers u
WHERE u.Email IN ('jim@teamsync.com', 'steve@teamsync.com')
ORDER BY ContributionCount DESC;

-- Step 2: Insert/Update AlertPreference for the user with most contributions
-- Replace 'USER_ID_HERE' with the actual user ID from Step 1 results (the one with most contributions)

DECLARE @UserId NVARCHAR(450) = (
	SELECT TOP 1 u.Id 
	FROM AspNetUsers u 
	WHERE u.Email IN ('jim@teamsync.com', 'steve@teamsync.com')
	ORDER BY (SELECT COUNT(*) FROM Contributions WHERE UserId = u.Id) DESC
);

-- Check if AlertPreference exists
IF EXISTS (SELECT 1 FROM AlertPreferences WHERE UserId = @UserId)
BEGIN
	UPDATE AlertPreferences
	SET 
		NotificationFrequency = 'Weekly',
		DigestDayOfWeek = DATEPART(dw, GETUTCDATE()) - 1,
		DigestHourUtc = DATEPART(hour, GETUTCDATE()) + 1,
		ReceiveTaskAssignmentAlerts = 1,
		ReceiveApprovalRejectionAlerts = 1,
		ReceiveStatusChangeAlerts = 1,
		ReceiveCommentAlerts = 1,
		ReceiveGroupAlerts = 1,
		UpdatedAt = GETUTCDATE()
	WHERE UserId = @UserId;
	PRINT 'Updated AlertPreference for user: ' + @UserId;
END
ELSE
BEGIN
	INSERT INTO AlertPreferences (
		UserId,
		NotificationFrequency,
		DigestDayOfWeek,
		DigestHourUtc,
		ReceiveTaskAssignmentAlerts,
		ReceiveApprovalRejectionAlerts,
		ReceiveStatusChangeAlerts,
		ReceiveCommentAlerts,
		ReceiveGroupAlerts,
		CreatedAt
	)
	VALUES (
		@UserId,
		'Weekly',
		DATEPART(dw, GETUTCDATE()) - 1,
		DATEPART(hour, GETUTCDATE()) + 1,
		1,
		1,
		1,
		1,
		1,
		GETUTCDATE()
	);
	PRINT 'Created AlertPreference for user: ' + @UserId;
END

-- Step 3: Verify the setup
SELECT 
	u.Email,
	u.FirstName,
	ap.NotificationFrequency,
	ap.DigestDayOfWeek,
	ap.DigestHourUtc,
	ap.ReceiveTaskAssignmentAlerts,
	ap.ReceiveApprovalRejectionAlerts,
	ap.ReceiveStatusChangeAlerts
FROM AspNetUsers u
LEFT JOIN AlertPreferences ap ON u.Id = ap.UserId
WHERE u.Email IN ('jim@teamsync.com', 'steve@teamsync.com');
