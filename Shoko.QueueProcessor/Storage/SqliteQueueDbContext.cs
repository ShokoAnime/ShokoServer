using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Shoko.QueueProcessor.Storage;

/// <summary>SQLite-backed queue database context.</summary>
public class SqliteQueueDbContext : QueueDbContext
{
    private readonly string _connectionString;

    // SQLite has no native DateTimeOffset type; store as Unix milliseconds (long) so that
    // ORDER BY clauses work correctly and the column is compact and sortable.
    private static readonly ValueConverter<DateTimeOffset, long> _dateTimeOffsetConverter =
        new(dto => dto.ToUnixTimeMilliseconds(),
            l => DateTimeOffset.FromUnixTimeMilliseconds(l));

    private static readonly ValueConverter<DateTimeOffset?, long?> _nullableDateTimeOffsetConverter =
        new(dto => dto.HasValue ? dto.Value.ToUnixTimeMilliseconds() : null,
            l => l.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(l.Value) : null);

    public SqliteQueueDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite(_connectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<QueuedJob>(entity =>
        {
            entity.Property(j => j.QueuedAt).HasConversion(_dateTimeOffsetConverter);
            entity.Property(j => j.ScheduledAt).HasConversion(_nullableDateTimeOffsetConverter);
        });
    }
}
