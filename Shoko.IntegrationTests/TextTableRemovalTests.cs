using System.Linq;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Checks that the released text tables, the old TMDB link tables and the text columns the text
/// store replaced are gone after the migration, and that the steps dropping them work on a
/// database that still has them, with rows in them.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TextTableRemovalTests(DatabaseMigrationFixture fixture)
{
    #region Tests

    [Fact]
    public void AMigratedDatabaseHasNoneOfTheReplacedTablesOrColumns()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        Assert.Empty(ReleasedTextSchema.Present(connection, fixture.Backend));
    }

    [Fact]
    public void TheDropStepsRemoveWhatAnUpgradedDatabaseStillHas()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        try
        {
            ReleasedTextSchema.Restore(connection, fixture.Backend);
            Assert.Equal(
                ReleasedTextSchema.Targets.Select(target => target.Column is null ? target.Table : $"{target.Table}.{target.Column}"),
                ReleasedTextSchema.Present(connection, fixture.Backend)
            );
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "INSERT INTO TMDB_Title (ParentID, ParentType, LanguageCode, CountryCode, Value) VALUES (1, 1, 'en', 'US', 'A title')";
                command.ExecuteNonQuery();
            }

            ReleasedTextSchema.Drop(fixture, connection);

            Assert.Empty(ReleasedTextSchema.Present(connection, fixture.Backend));
        }
        finally
        {
            ReleasedTextSchema.Drop(fixture, connection);
        }
    }

    #endregion
}
