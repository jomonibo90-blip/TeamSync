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
    }
}
