using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TeamSync.Models;

/// <summary>
/// Represents a file attachment associated with a task note/discussion comment.
/// Tracks uploaded files (images, PDFs, Word docs, etc.) in the discussion thread.
/// </summary>
public class FileAttachment
{
    public int Id { get; set; }

    [Required]
    public int TaskNoteId { get; set; }
    public TaskNote? TaskNote { get; set; }

    [Required]
    [StringLength(255)]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// MIME type of the file (e.g., "application/pdf", "image/png", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")
    /// </summary>
    [Required]
    [StringLength(100)]
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// File size in bytes
    /// </summary>
    [Required]
    public long FileSize { get; set; }

    /// <summary>
    /// Relative path to the stored file in the file system
    /// </summary>
    [Required]
    [StringLength(500)]
    public string FilePath { get; set; } = string.Empty;

    [Required]
    public string UploadedByUserId { get; set; } = string.Empty;
    public User? UploadedByUser { get; set; }

    [Required]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Determines if this is an image file that can be previewed in lightbox
    /// </summary>
    [NotMapped]
    public bool IsImage => !string.IsNullOrEmpty(FileType) && FileType.StartsWith("image/");

    /// <summary>
    /// Determines if this is a downloadable document (PDF, Word, etc.)
    /// </summary>
    [NotMapped]
    public bool IsDocument => !string.IsNullOrEmpty(FileType) && (
        FileType == "application/pdf" ||
        FileType.Contains("wordprocessingml") ||
        FileType.Contains("msword") ||
        FileType == "application/vnd.ms-word" ||
        FileType == "text/plain"
    );
}
