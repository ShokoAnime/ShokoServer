using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Shoko.QueueProcessor.Chain;

namespace Shoko.QueueProcessor.Storage.Contexts;

/// <summary>
/// Base EF Core <see cref="DbContext"/> for the queue. Provider-specific subclasses
/// call <c>UseSqlite</c>, <c>UseMySql</c>, or <c>UseSqlServer</c> in their constructors.
/// </summary>
public abstract class QueueDbContext : DbContext
{
    public DbSet<QueuedJob> Jobs { get; set; } = null!;
    public DbSet<QueuedJobChain> JobChains { get; set; } = null!;

    protected QueueDbContext() { }

    protected QueueDbContext(DbContextOptions options) : base(options) { }

    // SQLite has no native DateTimeOffset; all providers store as Unix milliseconds for
    // portability — ORDER BY works correctly and column is compact and sortable.
    private static readonly ValueConverter<DateTimeOffset, long> _dateTimeOffsetConverter =
        new(dto => dto.ToUnixTimeMilliseconds(),
            l => DateTimeOffset.FromUnixTimeMilliseconds(l));

    private static readonly ValueConverter<DateTimeOffset?, long?> _nullableDateTimeOffsetConverter =
        new(dto => dto.HasValue ? dto.Value.ToUnixTimeMilliseconds() : null,
            l => l.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(l.Value) : null);

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
            entity.Property(j => j.IsChainFinally).HasDefaultValue(false);

            entity.Property(j => j.QueuedAt).HasConversion(_dateTimeOffsetConverter);
            entity.Property(j => j.ScheduledAt).HasConversion(_nullableDateTimeOffsetConverter);

            // Startup load: grouped by type then ordered within pool sub-queue.
            // ORDER BY JobType, ScheduledAt, Priority DESC, QueuedAt ASC
            entity.HasIndex(j => new { j.JobType, j.ScheduledAt, j.Priority, j.QueuedAt })
                  .HasDatabaseName("IX_QueuedJobs_Startup");

            // Dedup safety net (primary dedup is in-memory via _jobKeyIndex)
            entity.HasIndex(j => j.JobKey).IsUnique()
                  .HasDatabaseName("IX_QueuedJobs_JobKey");

            entity.HasIndex(j => j.ChainId)
                  .HasDatabaseName("IX_QueuedJobs_ChainId");

            entity.HasIndex(j => j.ParentJobId)
                  .HasDatabaseName("IX_QueuedJobs_ParentJobId");
        });

        modelBuilder.Entity<QueuedJobChain>(entity =>
        {
            entity.HasKey(c => c.ChainId);
            entity.Property(c => c.ChainId).ValueGeneratedNever();
            entity.Property(c => c.Status).IsRequired();
            entity.Property(c => c.CreatedAt).IsRequired();
            entity.Property(c => c.UpdatedAt).IsRequired();
        });

    }
}
