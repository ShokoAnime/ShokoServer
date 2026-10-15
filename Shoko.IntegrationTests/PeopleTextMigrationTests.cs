using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the step that copies the names and descriptions creators and characters kept on their rows
/// into the text store, twice as an interrupted step is run again, and the steps dropping the columns.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class PeopleTextMigrationTests(DatabaseMigrationFixture fixture)
{
    private static readonly DateTime _created = new(2024, 1, 1, 10, 0, 0);

    [Fact]
    public void TheCopyStepPutsEachNameAndDescriptionFirstAndTheDropStepsRemoveTheColumns()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var plugin = MetadataNumberRegistry.GetNumber(TestSources.Plugin);
        var user = MetadataNumberRegistry.GetNumber(MetadataSource.User);
        var creator = MetadataNumberRegistry.GetNumber(MetadataEntityType.Creator);
        var character = MetadataNumberRegistry.GetNumber(MetadataEntityType.Character);
        var ids = "'ptm-c1', 'ptm-c2', 'ptm-x1'";
        try
        {
            ReleasedColumns.People.Restore(connection);
            void Person(string table, string id, string name, string? description)
                => Insert(
                    connection,
                    table,
                    ("Source", plugin),
                    ("ProviderID", id),
                    ("Name", name),
                    ("Description", description),
                    ("Type", 0),
                    ("CreatedAt", _created),
                    ("LastUpdatedAt", _created)
                );
            void Title(int entityType, string id, int source, string code, TitleType type, string value, int ordering)
                => Insert(
                    connection,
                    "Metadata_Title",
                    ("EntitySource", plugin),
                    ("EntityType", entityType),
                    ("EntityID", id),
                    ("Source", source),
                    ("Language", code),
                    ("LanguageCode", code),
                    ("TitleType", (int)type),
                    ("Value", value),
                    ("IsEnabled", true),
                    ("Preference", 0),
                    ("Ordering", ordering)
                );
            void Overview(int entityType, string id, string code, string value, int ordering)
                => Insert(
                    connection,
                    "Metadata_Overview",
                    ("EntitySource", plugin),
                    ("EntityType", entityType),
                    ("EntityID", id),
                    ("Source", plugin),
                    ("Language", code),
                    ("LanguageCode", code),
                    ("Value", value),
                    ("IsEnabled", true),
                    ("Preference", 0),
                    ("Ordering", ordering)
                );

            // A creator whose source stored other names and an overview, with a user's title beside them.
            Person("Metadata_Creator", "ptm-c1", "Kana", "About her.");
            Title(creator, "ptm-c1", plugin, "unk", TitleType.Synonym, "Kana-chan", 0);
            Title(creator, "ptm-c1", plugin, "ja", TitleType.Official, "花澤香菜", 1);
            Title(creator, "ptm-c1", user, "en", TitleType.Main, "Mine", 0);
            Overview(creator, "ptm-c1", "de", "Über sie.", 0);
            // A stub given no name, and a character with no description.
            Person("Metadata_Creator", "ptm-c2", " ", "  ");
            Person("Metadata_Character", "ptm-x1", "Alice", null);
            for (var run = 0; run < 2; run++)
                Run(DatabaseFixes.CopyPeopleTexts, connection);

            Assert.Equal(
                [
                    $"{creator}|ptm-c1|{plugin}|unk|{(int)TitleType.Main}|Kana|0",
                    $"{creator}|ptm-c1|{plugin}|unk|{(int)TitleType.Synonym}|Kana-chan|1",
                    $"{creator}|ptm-c1|{plugin}|ja|{(int)TitleType.Official}|花澤香菜|2",
                    $"{creator}|ptm-c1|{user}|en|{(int)TitleType.Main}|Mine|0",
                    $"{character}|ptm-x1|{plugin}|unk|{(int)TitleType.Main}|Alice|0",
                ],
                Read(
                    connection,
                    "SELECT EntityType, EntityID, Source, LanguageCode, TitleType, Value, Ordering FROM Metadata_Title " +
                    $"WHERE EntitySource = {plugin} AND EntityID IN ({ids}) ORDER BY EntityType, EntityID, Source, Ordering"
                )
            );
            Assert.Equal(
                ["ptm-c1|en|About her.|0", "ptm-c1|de|Über sie.|1"],
                Read(
                    connection,
                    "SELECT EntityID, LanguageCode, Value, Ordering FROM Metadata_Overview " +
                    $"WHERE EntitySource = {plugin} AND EntityID IN ({ids}) ORDER BY Ordering"
                )
            );

            foreach (var step in ReleasedColumns.People.DropSteps(fixture))
                Execute(connection, step.Command!);
            Assert.False(ReleasedColumns.People.AnyPresent(connection));
        }
        finally
        {
            ReleasedColumns.People.Drop(connection);
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {plugin} AND EntityID IN ({ids})");
            Execute(connection, $"DELETE FROM Metadata_Overview WHERE EntitySource = {plugin} AND EntityID IN ({ids})");
            Execute(connection, $"DELETE FROM Metadata_Creator WHERE Source = {plugin} AND ProviderID IN ({ids})");
            Execute(connection, $"DELETE FROM Metadata_Character WHERE Source = {plugin} AND ProviderID IN ({ids})");
        }
    }
}
