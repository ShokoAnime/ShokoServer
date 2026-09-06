using System;
using System.IO;
using System.Text.Json;
using Shoko.TestData.Schema;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Records the schema of the database this run migrated, for the cross-backend comparison in
/// <c>Shoko.Tests</c> to pick up.
/// </summary>
/// <remarks>
/// The three backends keep their own hand-written DDL, and nothing forces them to agree; comparing
/// them needs all three migrated, which is what this project's CI matrix already does. Each job
/// writes its dump and publishes it, and a later job collects the three and compares them, so no
/// recorded schema is kept in the repository to fall out of date.
///
/// The dump is written to the directory named by <see cref="SchemaDumps.DirectoryVariable"/>. When
/// that is unset there is nowhere to publish to and this only checks that the schema can be read at
/// all.
/// </remarks>
[Collection("Database")]
public class SchemaSnapshotTests(DatabaseMigrationFixture fixture) : IClassFixture<DatabaseMigrationFixture>
{
    [Fact]
    public void TheMigratedSchemaIsRecorded()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var schema = SchemaSnapshot.Read(connection, fixture.Backend);

        // A backend that reported almost nothing would otherwise be published as a dump the
        // comparison reads as a schema with nothing in it, and every column would look agreed.
        Assert.True(schema.Tables.Count > 60, $"{fixture.Backend}: only {schema.Tables.Count} tables.");

        if (Environment.GetEnvironmentVariable(SchemaDumps.DirectoryVariable) is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, SchemaDumps.FileNameFor(fixture.Backend)),
            JsonSerializer.Serialize(schema.Tables, new JsonSerializerOptions { WriteIndented = true }));
    }
}
