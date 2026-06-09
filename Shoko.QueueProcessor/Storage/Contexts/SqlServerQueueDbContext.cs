using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.QueueProcessor.Storage.Contexts;

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
            optionsBuilder
                .UseSqlServer(_connectionString)
                .ReplaceService<IMigrationsAssembly, QueueMigrationsAssembly>();
    }
}
