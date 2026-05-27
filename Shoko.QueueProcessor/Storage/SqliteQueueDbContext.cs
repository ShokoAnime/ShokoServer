using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage;

/// <summary>SQLite-backed queue database context.</summary>
public class SqliteQueueDbContext : QueueDbContext
{
    private readonly string _connectionString;

    public SqliteQueueDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlite(_connectionString);
    }
}
