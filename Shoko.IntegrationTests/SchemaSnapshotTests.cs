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
/// Written to the directory named by <see cref="SchemaDumps.DirectoryVariable"/>, which each CI job
/// publishes for a later job to compare. Unset, this only checks the schema can be read.
/// </remarks>
[Collection(DatabaseCollection.Name)]
public class SchemaSnapshotTests(DatabaseMigrationFixture fixture)
{
    [Fact]
    public void TheMigratedSchemaIsRecorded()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var schema = SchemaSnapshot.Read(connection, fixture.Backend);

        // A near-empty dump would make every column look agreed downstream.
        Assert.True(schema.Tables.Count > 60, $"{fixture.Backend}: only {schema.Tables.Count} tables.");

        if (Environment.GetEnvironmentVariable(SchemaDumps.DirectoryVariable) is not { Length: > 0 } directory)
            return;

        Directory.CreateDirectory(directory);
        File.WriteAllText(
            Path.Combine(directory, SchemaDumps.FileNameFor(fixture.Backend)),
            JsonSerializer.Serialize(schema.Tables, new JsonSerializerOptions { WriteIndented = true }));
    }
}
