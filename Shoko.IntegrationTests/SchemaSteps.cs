using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Server.Databases;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Finds the backend's own schema steps by the version and revision they were added under, which
/// never change once a release has them.
/// </summary>
internal static class SchemaSteps
{
    #region Public Methods

    /// <summary>
    /// The backend's steps of the release in the works, by their revisions.
    /// </summary>
    /// <param name="fixture">The started server.</param>
    /// <param name="revisions">The revisions, the same on every backend.</param>
    /// <returns>One step per revision, in the order asked for.</returns>
    public static IReadOnlyList<DatabaseCommand> Get(DatabaseMigrationFixture fixture, params int[] revisions)
    {
        var database = fixture.Services.GetRequiredService<DatabaseFactory>().Instance!;

        // The version the release in the works opened on each backend.
        var version = database switch
        {
            SQLite => 173,
            MySQL => 194,
            SQLServer => 192,
            _ => throw new NotSupportedException($"No schema version is known for {database.GetType().Name}."),
        };
        var field = database.GetType().GetField("_patchCommands", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var commands = (IEnumerable<DatabaseCommand>)field.GetValue(database)!;
        return revisions.Select(revision => Assert.Single(commands, command => command.Version == version && command.Revision == revision)).ToList();
    }

    /// <summary>
    /// The SQL of the backend's plain step of the release in the works with this revision.
    /// </summary>
    /// <param name="fixture">The started server.</param>
    /// <param name="revision">The revision, the same on every backend.</param>
    /// <returns>The step's SQL.</returns>
    public static string GetSql(DatabaseMigrationFixture fixture, int revision)
    {
        var step = Get(fixture, revision)[0];
        Assert.Equal(DatabaseCommandType.NormalCommand, step.Type);
        return step.Command!;
    }

    #endregion
}
