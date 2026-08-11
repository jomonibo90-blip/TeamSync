-- Create FileAttachments table on Azure SQL Database
-- Run this script against the TeamSyncDb database

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'FileAttachments')
BEGIN
	CREATE TABLE [dbo].[FileAttachments] (
		[Id] [int] IDENTITY(1,1) NOT NULL,
		[TaskNoteId] [int] NOT NULL,
		[FileName] [nvarchar](255) NOT NULL,
		[FileType] [nvarchar](100) NOT NULL,
		[FileSize] [bigint] NOT NULL,
		[FilePath] [nvarchar](500) NOT NULL,
		[UploadedByUserId] [nvarchar](450) NOT NULL,
		[UploadedAt] [datetime2] NOT NULL,
		CONSTRAINT [PK_FileAttachments] PRIMARY KEY CLUSTERED ([Id] ASC)
	);

	-- Create foreign key to AspNetUsers
	ALTER TABLE [dbo].[FileAttachments] 
	ADD CONSTRAINT [FK_FileAttachments_AspNetUsers_UploadedByUserId] 
	FOREIGN KEY([UploadedByUserId]) REFERENCES [dbo].[AspNetUsers]([Id])
	ON DELETE NO ACTION;

	-- Create foreign key to TaskNotes
	ALTER TABLE [dbo].[FileAttachments] 
	ADD CONSTRAINT [FK_FileAttachments_TaskNotes_TaskNoteId] 
	FOREIGN KEY([TaskNoteId]) REFERENCES [dbo].[TaskNotes]([Id])
	ON DELETE CASCADE;

	-- Create indexes
	CREATE NONCLUSTERED INDEX [IX_FileAttachments_TaskNoteId] 
	ON [dbo].[FileAttachments]([TaskNoteId]);

	CREATE NONCLUSTERED INDEX [IX_FileAttachments_UploadedByUserId] 
	ON [dbo].[FileAttachments]([UploadedByUserId]);

	PRINT 'FileAttachments table created successfully.';
END
ELSE
BEGIN
	PRINT 'FileAttachments table already exists.';
END
