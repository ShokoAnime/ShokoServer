using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the backend's vacuum (behind the <c>Vacuum Database</c> action) and SQLite's
/// start-up compaction against the migrated database, and checks it still answers afterwards.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DatabaseVacuumTests(DatabaseMigrationFixture fixture)
{
    #region Tests

    [Fact]
    public void TheVacuumRunsOnTheMigratedDatabase()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var database = fixture.Services.GetRequiredService<DatabaseFactory>().Instance!;

        database.Vacuum();

        Assert.Equal(database.RequiredVersion, database.GetDatabaseVersion());
        using var connection = fixture.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Versions";
        Assert.True(Convert.ToInt64(command.ExecuteScalar()) > 0);
    }

    [Fact]
    public void TheStartupCompactionMeasuresTheMigratedDatabaseWithoutFailing()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        var database = fixture.Services.GetRequiredService<DatabaseFactory>().Instance!;
        Assert.SkipUnless(database is SQLite, "Only SQLite is measured; the other backends are unit tested.");
        var sqlite = (SQLite)database;

        var decision = StartupCompaction.Run(sqlite, true, NullLogger.Instance, _ => { });

        Assert.NotEqual(StartupCompactionDecision.Failed, decision);
        var space = sqlite.MeasureSpace();
        Assert.True(space.DatabaseSize > 0);
        Assert.InRange(space.ReclaimableBytes, 0, space.DatabaseSize);
        Assert.True(space.SizeOnDisk > 0);
        Assert.Equal(database.RequiredVersion, database.GetDatabaseVersion());
    }

    #endregion
}
