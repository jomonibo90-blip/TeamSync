using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace TeamSync.Data;

public class ApplicationDbContext : IdentityDbContext<Models.User>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Models.Group> Groups { get; set; }
    public DbSet<Models.GroupMember> GroupMembers { get; set; }
    public DbSet<Models.Task> Tasks { get; set; }
    public DbSet<Models.Contribution> Contributions { get; set; }
    public DbSet<Models.RemovalRequest> RemovalRequests { get; set; }
    public DbSet<Models.AddMemberRequest> AddMemberRequests { get; set; }
    public DbSet<Models.JoinRequest> JoinRequests { get; set; }
    public DbSet<Models.TaskAssignment> TaskAssignments { get; set; }
    public DbSet<Models.TaskNote> TaskNotes { get; set; }

    // Contribution history audit
    public DbSet<Models.ContributionHistory> ContributionHistories { get; set; }

    // Contribution overrides (immutable student records with lead overrides)
    public DbSet<Models.ContributionOverride> ContributionOverrides { get; set; }

    // Real-time notifications
    public DbSet<Models.Notification> Notifications { get; set; }

    // Alert preferences for email notifications
    public DbSet<Models.AlertPreference> AlertPreferences { get; set; }

    // Group chat messages
    public DbSet<Models.ChatMessage> ChatMessages { get; set; }

    // File attachments for task notes
    public DbSet<Models.FileAttachment> FileAttachments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Group relationships
        modelBuilder.Entity<Models.Group>()
            .HasOne(g => g.CreatedBy)
            .WithMany(u => u.CreatedGroups)
            .HasForeignKey(g => g.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Models.Group>()
            .HasMany(g => g.Members)
            .WithOne(gm => gm.Group)
            .HasForeignKey(gm => gm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        // When a Group is deleted, set Task.GroupId to NULL rather than cascade-delete tasks
        modelBuilder.Entity<Models.Group>()
            .HasMany(g => g.Tasks)
            .WithOne(t => t.Group)
            .HasForeignKey(t => t.GroupId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure GroupMember relationships
        modelBuilder.Entity<Models.GroupMember>()
            .HasOne(gm => gm.User)
            .WithMany(u => u.GroupMembers)
            .HasForeignKey(gm => gm.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Task relationships
        modelBuilder.Entity<Models.Task>()
            .HasOne(t => t.CreatedBy)
            .WithMany(u => u.CreatedTasks)
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Models.Task>()
            .HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Models.Task>()
            .HasOne(t => t.ArchivedBy)
            .WithMany()
            .HasForeignKey(t => t.ArchivedById)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.Task>()
            .HasMany(t => t.Contributions)
            .WithOne(c => c.Task)
            .HasForeignKey(c => c.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure Contribution relationships
        modelBuilder.Entity<Models.Contribution>()
            .HasOne(c => c.User)
            .WithMany(u => u.Contributions)
            .HasForeignKey(c => c.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // RecordedBy relationship (user who logged the contribution)
        modelBuilder.Entity<Models.Contribution>()
            .HasOne(c => c.RecordedBy)
            .WithMany()
            .HasForeignKey(c => c.RecordedById)
            .OnDelete(DeleteBehavior.NoAction);

        // Configure HoursSpent decimal precision
        modelBuilder.Entity<Models.Contribution>()
            .Property(c => c.HoursSpent)
            .HasPrecision(18, 2);

        // Prevent duplicate contributions per task+user
        modelBuilder.Entity<Models.Contribution>()
            .HasIndex(c => new { c.TaskId, c.UserId })
            .IsUnique();

        // Configure ContributionOverride relationships
        modelBuilder.Entity<Models.ContributionOverride>()
            .HasOne(co => co.Contribution)
            .WithMany(c => c.Overrides)
            .HasForeignKey(co => co.ContributionId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.ContributionOverride>()
            .HasOne(co => co.OverriddenBy)
            .WithMany()
            .HasForeignKey(co => co.OverriddenById)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.ContributionOverride>()
            .HasOne(co => co.DisputedBy)
            .WithMany()
            .HasForeignKey(co => co.DisputedById)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure override decimal precision
        modelBuilder.Entity<Models.ContributionOverride>()
            .Property(co => co.OriginalHours)
            .HasPrecision(18, 2);
        modelBuilder.Entity<Models.ContributionOverride>()
            .Property(co => co.NewHours)
            .HasPrecision(18, 2);

        // Configure RemovalRequest relationships
        modelBuilder.Entity<Models.RemovalRequest>()
            .HasOne(rr => rr.GroupMember)
            .WithMany()
            .HasForeignKey(rr => rr.GroupMemberId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasOne(rr => rr.Group)
            .WithMany()
            .HasForeignKey(rr => rr.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasOne(rr => rr.User)
            .WithMany()
            .HasForeignKey(rr => rr.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasOne(rr => rr.RequestedBy)
            .WithMany()
            .HasForeignKey(rr => rr.RequestedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasOne(rr => rr.ApprovedBy)
            .WithMany()
            .HasForeignKey(rr => rr.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure AddMemberRequest relationships
        modelBuilder.Entity<Models.AddMemberRequest>()
            .HasOne(amr => amr.Group)
            .WithMany()
            .HasForeignKey(amr => amr.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.AddMemberRequest>()
            .HasOne(amr => amr.User)
            .WithMany()
            .HasForeignKey(amr => amr.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.AddMemberRequest>()
            .HasOne(amr => amr.RequestedBy)
            .WithMany()
            .HasForeignKey(amr => amr.RequestedByUserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.AddMemberRequest>()
            .HasOne(amr => amr.ApprovedBy)
            .WithMany()
            .HasForeignKey(amr => amr.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure JoinRequest relationships
        modelBuilder.Entity<Models.JoinRequest>()
            .HasOne(jr => jr.Group)
            .WithMany()
            .HasForeignKey(jr => jr.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.JoinRequest>()
            .HasOne(jr => jr.User)
            .WithMany()
            .HasForeignKey(jr => jr.UserId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.JoinRequest>()
            .HasOne(jr => jr.ApprovedBy)
            .WithMany()
            .HasForeignKey(jr => jr.ApprovedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Configure TaskAssignment relationships
        modelBuilder.Entity<Models.TaskAssignment>()
            .HasOne(ta => ta.Task)
            .WithMany(t => t.Assignments)
            .HasForeignKey(ta => ta.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.TaskAssignment>()
            .HasOne(ta => ta.AssignedTo)
            .WithMany(u => u.TaskAssignments)
            .HasForeignKey(ta => ta.AssignedToId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Models.TaskAssignment>()
            .HasOne(ta => ta.AssignedByUser)
            .WithMany()
            .HasForeignKey(ta => ta.AssignedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure TaskNote relationships
        modelBuilder.Entity<Models.TaskNote>()
            .HasOne(tn => tn.Task)
            .WithMany(t => t.Notes)
            .HasForeignKey(tn => tn.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.TaskNote>()
            .HasOne(tn => tn.User)
            .WithMany(u => u.TaskNotes)
            .HasForeignKey(tn => tn.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Configure ContributionHistory
        modelBuilder.Entity<Models.ContributionHistory>()
            .HasOne(ch => ch.Contribution)
            .WithMany()
            .HasForeignKey(ch => ch.ContributionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.ContributionHistory>()
            .HasIndex(ch => ch.ContributionId);

        // Add indexes for common queries
        modelBuilder.Entity<Models.Group>()
            .HasIndex(g => g.CreatedById);

        modelBuilder.Entity<Models.GroupMember>()
            .HasIndex(gm => gm.GroupId);

        modelBuilder.Entity<Models.GroupMember>()
            .HasIndex(gm => gm.UserId);

        modelBuilder.Entity<Models.Task>()
            .HasIndex(t => t.GroupId);

        modelBuilder.Entity<Models.Task>()
            .HasIndex(t => t.AssignedToId);

        modelBuilder.Entity<Models.Contribution>()
            .HasIndex(c => c.TaskId);

        modelBuilder.Entity<Models.Contribution>()
            .HasIndex(c => c.UserId);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasIndex(rr => rr.GroupId);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasIndex(rr => rr.UserId);

        modelBuilder.Entity<Models.RemovalRequest>()
            .HasIndex(rr => rr.Status);

        // Configure Notification relationships
        modelBuilder.Entity<Models.Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.Notification>()
            .HasOne(n => n.Task)
            .WithMany()
            .HasForeignKey(n => n.TaskId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indices for notification queries
        modelBuilder.Entity<Models.Notification>()
            .HasIndex(n => n.UserId);

        modelBuilder.Entity<Models.Notification>()
            .HasIndex(n => n.IsRead);

        modelBuilder.Entity<Models.Notification>()
            .HasIndex(n => new { n.UserId, n.IsRead });

        // Configure ChatMessage relationships
        modelBuilder.Entity<Models.ChatMessage>()
            .HasOne(cm => cm.Group)
            .WithMany(g => g.ChatMessages)
            .HasForeignKey(cm => cm.GroupId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.ChatMessage>()
            .HasOne(cm => cm.Sender)
            .WithMany()
            .HasForeignKey(cm => cm.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indices for chat message queries
        modelBuilder.Entity<Models.ChatMessage>()
            .HasIndex(cm => cm.GroupId);

        modelBuilder.Entity<Models.ChatMessage>()
            .HasIndex(cm => cm.CreatedAt);

        modelBuilder.Entity<Models.ChatMessage>()
            .HasIndex(cm => new { cm.GroupId, cm.CreatedAt });

        // Configure AlertPreference relationships
        modelBuilder.Entity<Models.AlertPreference>()
            .HasOne(ap => ap.User)
            .WithOne(u => u.AlertPreference)
            .HasForeignKey<Models.AlertPreference>(ap => ap.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Indices for alert preference queries
        modelBuilder.Entity<Models.AlertPreference>()
            .HasIndex(ap => ap.UserId);

        // Configure FileAttachment relationships
        modelBuilder.Entity<Models.FileAttachment>()
            .HasOne(fa => fa.TaskNote)
            .WithMany(tn => tn.Attachments)
            .HasForeignKey(fa => fa.TaskNoteId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Models.FileAttachment>()
            .HasOne(fa => fa.UploadedByUser)
            .WithMany()
            .HasForeignKey(fa => fa.UploadedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indices for file attachment queries
        modelBuilder.Entity<Models.FileAttachment>()
            .HasIndex(fa => fa.TaskNoteId);

        modelBuilder.Entity<Models.FileAttachment>()
            .HasIndex(fa => fa.UploadedByUserId);
    }
}
