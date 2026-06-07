using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Shoko.QueueProcessor.Storage;

/// <summary>MySQL/MariaDB-backed queue database context (via Pomelo).</summary>
public class MySqlQueueDbContext : QueueDbContext
{
    private readonly string _connectionString;

    public MySqlQueueDbContext(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder
                .UseMySql(_connectionString, ServerVersion.AutoDetect(_connectionString))
                .ReplaceService<IMigrationsAssembly, QueueMigrationsAssembly>();
    }
}
