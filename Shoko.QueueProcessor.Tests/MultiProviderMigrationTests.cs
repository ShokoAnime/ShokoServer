using System.Linq;
using Microsoft.EntityFrameworkCore;
using Shoko.QueueProcessor.Storage;
using Shoko.QueueProcessor.Storage.Contexts;
using Xunit;

namespace Shoko.QueueProcessor.Tests;

/// <summary>
/// Verifies that non-SQLite provider contexts expose the same migration set as
/// <see cref="SqliteQueueDbContext"/> via <see cref="QueueMigrationsAssembly"/>.
/// <para>
/// The root cause this guards against: EF Core's default <c>IMigrationsAssembly</c>
/// filters by exact <c>[DbContext]</c> type. All migration Designer files are tagged
/// <c>[DbContext(typeof(SqliteQueueDbContext))]</c>, so without the override a
/// <see cref="SqlServerQueueDbContext"/> would find zero migrations, skip schema
/// creation, and crash on the first repository call.
/// </para>
/// These tests use <c>Database.GetMigrations()</c>, which reads assembly metadata and
/// requires no live database connection.
/// </summary>
public class MultiProviderMigrationTests
{
    [Fact]
    public void SqlServerContext_ExposesSameMigrationsAsSqlite()
    {
        using var sqliteCtx = new SqliteQueueDbContext("Data Source=:memory:");
        using var sqlServerCtx = new SqlServerQueueDbContext("Server=.;Database=_migrations_test;Trusted_Connection=false;");

        var expected = sqliteCtx.Database.GetMigrations().ToList();
        var actual = sqlServerCtx.Database.GetMigrations().ToList();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }
}
