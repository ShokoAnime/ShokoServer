using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage;

/// <summary>SQL Server-backed queue database context.</summary>
public class SqlServerQueueDbContext : QueueDbContext
{
    private readonly string _connectionString;

    public SqlServerQueueDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(_connectionString);
    }
}
