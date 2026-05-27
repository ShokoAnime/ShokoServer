using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage;

/// <summary>
/// Base EF Core <see cref="DbContext"/> for the queue. Provider-specific subclasses
/// call <c>UseSqlite</c>, <c>UseMySql</c>, or <c>UseSqlServer</c> in their constructors.
/// </summary>
public abstract class QueueDbContext : DbContext
{
    public DbSet<QueuedJob> Jobs { get; set; } = null!;

    protected QueueDbContext() { }

    protected QueueDbContext(DbContextOptions options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueuedJob>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.Id).ValueGeneratedNever();

            entity.Property(j => j.JobType).IsRequired().HasMaxLength(256);
            entity.Property(j => j.JobKey).IsRequired().HasMaxLength(512);
            entity.Property(j => j.JobDataJson).HasMaxLength(4096);
            entity.Property(j => j.Priority).HasDefaultValue(0);
            entity.Property(j => j.RetryCount).HasDefaultValue(0);

            // Startup load: grouped by type then ordered within pool sub-queue.
            // ORDER BY JobType, ScheduledAt, Priority DESC, QueuedAt ASC
            entity.HasIndex(j => new { j.JobType, j.ScheduledAt, j.Priority, j.QueuedAt })
                  .HasDatabaseName("IX_QueuedJobs_Startup");

            // Dedup safety net (primary dedup is in-memory via _jobKeyIndex)
            entity.HasIndex(j => j.JobKey).IsUnique()
                  .HasDatabaseName("IX_QueuedJobs_JobKey");
        });
    }
}
