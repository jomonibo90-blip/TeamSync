-- Direct data sync from Azure to Local
-- This script syncs all capstone demo data from production to local development

SET QUOTED_IDENTIFIER ON
SET ANSI_NULLS ON

PRINT '========================================='
PRINT 'TeamSync Azure to Local Data Sync'
PRINT '========================================='

-- Create linked server to Azure SQL
EXEC sp_addlinkedserver 
	@server = 'AZURE_PROD',
	@srvproduct = 'SQL Server',
	@provider = 'SQLNCLI', 
	@datasrc = 'teamsync-prod-sql-2026.database.windows.net,1433',
	@catalog = 'TeamSyncDb'

EXEC sp_addlinkedsrvlogin 
	@rmtsrvname = 'AZURE_PROD',
	@useself = 'false',
	@rmtuser = 'teamsyncadmin',
	@rmtpassword = 'xpress23@'

PRINT 'Linked server created'
PRINT ''

-- Disable constraints
EXEC sp_MSForEachTable 'ALTER TABLE ? NOCHECK CONSTRAINT ALL'
PRINT 'Constraints disabled'

-- Clear local data
PRINT 'Clearing local data...'
DELETE FROM [Contributions]
DELETE FROM [ChatMessages]
DELETE FROM [FileAttachments]
DELETE FROM [Notifications]
DELETE FROM [AlertPreferences]
DELETE FROM [ContributionOverrides]
DELETE FROM [ContributionHistories]
DELETE FROM [TaskNotes]
DELETE FROM [TaskAssignments]
DELETE FROM [Tasks]
DELETE FROM [RemovalRequests]
DELETE FROM [AddMemberRequests]
DELETE FROM [JoinRequests]
DELETE FROM [GroupMembers]
DELETE FROM [Groups]
DELETE FROM [AspNetUserTokens]
DELETE FROM [AspNetUserRoles]
DELETE FROM [AspNetUserClaims]
DELETE FROM [AspNetUserLogins]
DELETE FROM [AspNetUsers]

PRINT '✓ Local data cleared'
PRINT ''

-- Sync Identity tables
PRINT 'Syncing AspNetRoles...'
INSERT INTO [AspNetRoles]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetRoles')
PRINT '✓ AspNetRoles synced'

PRINT 'Syncing AspNetUsers...'
INSERT INTO [AspNetUsers]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetUsers')
PRINT '✓ AspNetUsers synced'

PRINT 'Syncing AspNetUserRoles...'
INSERT INTO [AspNetUserRoles]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetUserRoles')
PRINT '✓ AspNetUserRoles synced'

PRINT 'Syncing AspNetUserClaims...'
INSERT INTO [AspNetUserClaims]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetUserClaims')
PRINT '✓ AspNetUserClaims synced'

PRINT 'Syncing AspNetUserLogins...'
INSERT INTO [AspNetUserLogins]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetUserLogins')
PRINT '✓ AspNetUserLogins synced'

PRINT 'Syncing AspNetUserTokens...'
INSERT INTO [AspNetUserTokens]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AspNetUserTokens')
PRINT '✓ AspNetUserTokens synced'

-- Sync domain tables
PRINT ''
PRINT 'Syncing Groups...'
INSERT INTO [Groups]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM Groups')
PRINT '✓ Groups synced'

PRINT 'Syncing GroupMembers...'
INSERT INTO [GroupMembers]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM GroupMembers')
PRINT '✓ GroupMembers synced'

PRINT 'Syncing Tasks...'
INSERT INTO [Tasks]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM Tasks')
PRINT '✓ Tasks synced'

PRINT 'Syncing TaskAssignments...'
INSERT INTO [TaskAssignments]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM TaskAssignments')
PRINT '✓ TaskAssignments synced'

PRINT 'Syncing TaskNotes...'
INSERT INTO [TaskNotes]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM TaskNotes')
PRINT '✓ TaskNotes synced'

PRINT 'Syncing Contributions...'
INSERT INTO [Contributions]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM Contributions')
PRINT '✓ Contributions synced'

PRINT 'Syncing ContributionHistories...'
INSERT INTO [ContributionHistories]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM ContributionHistories')
PRINT '✓ ContributionHistories synced'

PRINT 'Syncing ContributionOverrides...'
INSERT INTO [ContributionOverrides]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM ContributionOverrides')
PRINT '✓ ContributionOverrides synced'

PRINT 'Syncing Notifications...'
INSERT INTO [Notifications]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM Notifications')
PRINT '✓ Notifications synced'

PRINT 'Syncing AlertPreferences...'
INSERT INTO [AlertPreferences]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AlertPreferences')
PRINT '✓ AlertPreferences synced'

PRINT 'Syncing ChatMessages...'
INSERT INTO [ChatMessages]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM ChatMessages')
PRINT '✓ ChatMessages synced'

PRINT 'Syncing FileAttachments...'
INSERT INTO [FileAttachments]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM FileAttachments')
PRINT '✓ FileAttachments synced'

PRINT 'Syncing JoinRequests...'
INSERT INTO [JoinRequests]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM JoinRequests')
PRINT '✓ JoinRequests synced'

PRINT 'Syncing AddMemberRequests...'
INSERT INTO [AddMemberRequests]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM AddMemberRequests')
PRINT '✓ AddMemberRequests synced'

PRINT 'Syncing RemovalRequests...'
INSERT INTO [RemovalRequests]
SELECT * FROM OPENQUERY(AZURE_PROD, 'SELECT * FROM RemovalRequests')
PRINT '✓ RemovalRequests synced'

-- Re-enable constraints
PRINT ''
PRINT 'Re-enabling constraints...'
EXEC sp_MSForEachTable 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL'
PRINT '✓ Constraints re-enabled'

-- Cleanup
EXEC sp_dropserver 'AZURE_PROD', 'droplogins'

PRINT ''
PRINT '========================================='
PRINT 'Data sync completed successfully!'
PRINT '========================================='
PRINT 'Your local database now contains the complete production data.'
PRINT 'You can now call /api/seed/send-digest-now to send the weekly digest email!'
