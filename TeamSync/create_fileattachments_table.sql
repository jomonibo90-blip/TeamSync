-- Create FileAttachments table
CREATE TABLE [FileAttachments] (
	[Id] int NOT NULL IDENTITY(1, 1),
	[TaskNoteId] int NOT NULL,
	[FileName] nvarchar(255) NOT NULL,
	[FileType] nvarchar(100) NOT NULL,
	[FileSize] bigint NOT NULL,
	[FilePath] nvarchar(500) NOT NULL,
	[UploadedByUserId] nvarchar(450) NOT NULL,
	[UploadedAt] datetime2 NOT NULL,
	CONSTRAINT [PK_FileAttachments] PRIMARY KEY ([Id]),
	CONSTRAINT [FK_FileAttachments_AspNetUsers_UploadedByUserId] FOREIGN KEY ([UploadedByUserId])
		REFERENCES [AspNetUsers]([Id]) ON DELETE NO ACTION,
	CONSTRAINT [FK_FileAttachments_TaskNotes_TaskNoteId] FOREIGN KEY ([TaskNoteId])
		REFERENCES [TaskNotes]([Id]) ON DELETE CASCADE
);

-- Create indexes
CREATE INDEX [IX_FileAttachments_TaskNoteId] ON [FileAttachments]([TaskNoteId]);
CREATE INDEX [IX_FileAttachments_UploadedByUserId] ON [FileAttachments]([UploadedByUserId]);
