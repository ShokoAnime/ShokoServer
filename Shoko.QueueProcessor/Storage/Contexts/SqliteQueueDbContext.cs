using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Shoko.QueueProcessor.Storage.Contexts;

/// <summary>SQLite-backed queue database context.</summary>
public class SqliteQueueDbContext : QueueDbContext
{
    private readonly string _connectionString;

    public SqliteQueueDbContext(string connectionString)
    {
        _connectionString = connectionString;
        EnsureDirectoryExists(connectionString);
    }

    /// <summary>
    /// Ensures the directory containing the SQLite database file exists before EF Core
    /// attempts to create or open it. <see cref="Directory.CreateDirectory(string)"/> is a no-op
    /// when the directory is already present.
    /// </summary>
    private static void EnsureDirectoryExists(string connectionString)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;
        if (string.IsNullOrEmpty(dataSource)) return;

        var dir = Path.GetDirectoryName(Path.GetFullPath(dataSource));
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder
                .UseSqlite(_connectionString)
                .AddInterceptors(new SqlitePragmaConnectionInterceptor());
    }
}
