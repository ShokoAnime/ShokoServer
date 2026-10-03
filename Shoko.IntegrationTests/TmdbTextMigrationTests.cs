using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Databases;
using Shoko.Server.Server;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the steps that copy TMDB's titles, overviews and people's other names
/// out of the released tables into the text store, and checks every copied
/// value and which entries listed their English text.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbTextMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int ShowID = 987_801;

    private const int RenamedShowID = 987_802;

    private const int EpisodeID = 987_811;

    private const int UnlistedEpisodeID = 987_812;

    private const int SeasonID = 987_831;

    private const int MovieID = 987_821;

    private const int CollectionID = 987_841;

    private const int PersonID = 987_851;

    private const int NamelessPersonID = 987_852;

    private static readonly int[] _ids = [ShowID, RenamedShowID, EpisodeID, UnlistedEpisodeID, SeasonID, MovieID, CollectionID, PersonID, NamelessPersonID];

    /// <summary>
    /// A row of the old title or overview table.
    /// </summary>
    private sealed record OldRow(ForeignEntityType Type, int ID, string Language, string Country, string Value);

    /// <summary>
    /// The old titles, in the order they were written.
    /// </summary>
    private static readonly OldRow[] _oldTitles =
    [
        new(ForeignEntityType.Show, ShowID, "ja", "JP", "番組"),
        new(ForeignEntityType.Show, ShowID, "en", "US", "The Show"),
        new(ForeignEntityType.Show, ShowID, "fr", "FR", "Le Show"),
        new(ForeignEntityType.Show, ShowID, "en", "GB", "The Show UK"),
        new(ForeignEntityType.Show, RenamedShowID, "de", "DE", "Die Show"),
        new(ForeignEntityType.Show, RenamedShowID, "en", "US", "Old Name"),
        new(ForeignEntityType.Episode, EpisodeID, "en", "US", "Episode 5"),
        new(ForeignEntityType.Episode, EpisodeID, "ja", "JP", "第5話"),
        new(ForeignEntityType.Episode, EpisodeID, "ko", "KR", "진짜 제목"),
        new(ForeignEntityType.Episode, EpisodeID, "zh", "CN", "第7集"),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "ja", "JP", "本当"),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "fr", "", "Titre"),
        new(ForeignEntityType.Season, SeasonID, "en", "US", "Season 1"),
        new(ForeignEntityType.Season, SeasonID, "en", "GB", "Season 1"),
        new(ForeignEntityType.Season, SeasonID, "de", "DE", "Staffel 1"),
        new(ForeignEntityType.Movie, MovieID, "fr", "FR", "Le Film"),
        new(ForeignEntityType.Movie, MovieID, "ja", "JP", "映画"),
        new(ForeignEntityType.Movie, MovieID, "en", "US", "The Film"),
        new(ForeignEntityType.Collection, CollectionID, "de", "DE", "Die Sammlung"),
        new(ForeignEntityType.Collection, CollectionID, "en", "US", "The Collection"),
    ];

    /// <summary>
    /// The old overviews, in the order they were written.
    /// </summary>
    private static readonly OldRow[] _oldOverviews =
    [
        new(ForeignEntityType.Show, ShowID, "en", "US", "An overview."),
        new(ForeignEntityType.Show, ShowID, "de", "DE", "Eine Übersicht."),
        new(ForeignEntityType.Show, RenamedShowID, "ja", "JP", "概要"),
        new(ForeignEntityType.Episode, EpisodeID, "fr", "FR", "Un résumé."),
        new(ForeignEntityType.Episode, EpisodeID, "en", "US", "What happens."),
        new(ForeignEntityType.Episode, UnlistedEpisodeID, "en", "GB", "British words."),
        new(ForeignEntityType.Movie, MovieID, "en", "US", "Stale text."),
        new(ForeignEntityType.Collection, CollectionID, "en", "US", "All of them."),
        new(ForeignEntityType.Person, PersonID, "ja", "JP", "経歴"),
        new(ForeignEntityType.Person, PersonID, "en", "US", "A life."),
    ];

    #endregion

    #region Helpers

    private static List<object?[]> ReadRows(IDbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<object?[]>();
        while (reader.Read())
        {
            var values = new object?[reader.FieldCount];
            for (var index = 0; index < values.Length; index++)
                values[index] = reader.IsDBNull(index) ? null : reader.GetValue(index);
            rows.Add(values);
        }

        return rows;
    }

    private void InsertOld(IDbConnection connection, string table, IEnumerable<OldRow> rows)
    {
        foreach (var row in rows)
            Execute(
                connection,
                $"INSERT INTO {table} (ParentID, ParentType, LanguageCode, CountryCode, Value) VALUES (@id, @type, @language, @country, @value)",
                ("@id", row.ID),
                ("@type", (int)row.Type),
                ("@language", row.Language),
                ("@country", row.Country),
                ("@value", row.Value)
            );
    }

    private void Cleanup(IDbConnection connection)
    {
        var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
        foreach (var id in _ids)
        {
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {tmdb} AND EntityID = '{id}'");
            Execute(connection, $"DELETE FROM Metadata_Overview WHERE EntitySource = {tmdb} AND EntityID = '{id}'");
        }
    }

    #endregion

    #region Tests

    [Fact]
    public void TheCopyStepsKeepEveryValueAndEveryRouteAnswersAsBefore()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);
        using var connection = fixture.OpenConnection();
        var restored = ReleasedTmdbSchema.Restore(connection, fixture.Backend);
        try
        {
            // What a database upgraded from a released version holds until the drop steps run.
            ReleasedTextSchema.Restore(connection, fixture.Backend);
            var now = new DateTime(2024, 1, 1);
            void Show(int id, string title, string overview)
                => Insert(connection, "TMDB_Show", ("TmdbShowID", id), ("EnglishTitle", title), ("EnglishOverview", overview), ("OriginalTitle", title), ("OriginalLanguageCode", "ja"),
                    ("IsRestricted", false), ("Genres", string.Empty), ("ContentRatings", string.Empty), ("EpisodeCount", 0), ("SeasonCount", 0), ("AlternateOrderingCount", 0),
                    ("UserRating", 0), ("UserVotes", 0), ("CreatedAt", now), ("LastUpdatedAt", now));
            void Episode(int id, int number, string title, string overview)
                => Insert(connection, "TMDB_Episode", ("TmdbShowID", ShowID), ("TmdbSeasonID", SeasonID), ("TmdbEpisodeID", id), ("EnglishTitle", title), ("EnglishOverview", overview),
                    ("SeasonNumber", 1), ("EpisodeNumber", number), ("UserRating", 0), ("UserVotes", 0), ("CreatedAt", now), ("LastUpdatedAt", now), ("IsHidden", false));
            void Person(int id, string name, string biography, string aliases)
                => Insert(connection, "TMDB_Person", ("TmdbPersonID", id), ("EnglishName", name), ("EnglishBiography", biography), ("Gender", 0), ("IsRestricted", false),
                    ("CreatedAt", now), ("LastUpdatedAt", now), ("Aliases", aliases));
            Show(ShowID, "The Show", "An overview.");
            Show(RenamedShowID, "New Name", "Its overview.");
            Episode(EpisodeID, 5, "Episode 5", "What happens.");
            Episode(UnlistedEpisodeID, 6, "Real Title", "Unlisted overview.");
            Insert(connection, "TMDB_Season", ("TmdbShowID", ShowID), ("TmdbSeasonID", SeasonID), ("EnglishTitle", "Season 1"), ("EnglishOverview", string.Empty), ("EpisodeCount", 2),
                ("SeasonNumber", 1), ("CreatedAt", now), ("LastUpdatedAt", now));
            Insert(connection, "TMDB_Movie", ("TmdbMovieID", MovieID), ("EnglishTitle", "The Film"), ("EnglishOverview", "Fresh text."), ("OriginalTitle", "The Film"),
                ("OriginalLanguageCode", "en"), ("IsRestricted", false), ("IsVideo", false), ("Genres", string.Empty), ("ContentRatings", string.Empty), ("UserRating", 0), ("UserVotes", 0),
                ("CreatedAt", now), ("LastUpdatedAt", now));
            Insert(connection, "TMDB_Collection", ("TmdbCollectionID", CollectionID), ("EnglishTitle", "The Collection"), ("EnglishOverview", "All of them."), ("MovieCount", 0),
                ("CreatedAt", now), ("LastUpdatedAt", now));
            Person(PersonID, "Some Person", "A life.", "First Alias||||||Second/Alias");
            Person(NamelessPersonID, "No Aliases", string.Empty, string.Empty);
            InsertOld(connection, "TMDB_Title", _oldTitles);
            InsertOld(connection, "TMDB_Overview", _oldOverviews);

            // Twice, as a step interrupted once is run again from the start.
            for (var run = 0; run < 2; run++)
            {
                Run(DatabaseFixes.MigrateTmdbTitles, connection);
                Run(DatabaseFixes.MigrateTmdbOverviews, connection);
                Run(DatabaseFixes.MigrateTmdbPersonAliases, connection);
            }

            // Every copied value, exactly.
            var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
            string Kind(MetadataEntityType type) => MetadataNumberRegistry.GetNumber(type).ToString();
            var titleRows = ReadRows(connection, $"SELECT EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, TitleType, Value, IsEnabled, Preference, Ordering, ReferenceID FROM Metadata_Title WHERE EntitySource = {tmdb} AND EntityID IN ({string.Join(", ", _ids.Select(id => $"'{id}'"))}) ORDER BY EntityType, EntityID, Ordering")
                .Select(row => string.Join("|", row.Select(value => value is null ? "NULL" : value is bool flag ? (flag ? "1" : "0") : Convert.ToString(value))))
                .ToList();
            string Row(MetadataEntityType type, int id, string language, string code, string? country, TitleType titleType, string value, int ordering)
                => $"{tmdb}|{Kind(type)}|{id}|{tmdb}|{language}|{code}|{country ?? "NULL"}|NULL|{(int)titleType}|{value}|1|0|{ordering}|NULL";
            var expectedTitles = new[]
            {
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "ja", "ja", "JP", TitleType.Official, "番組", 0)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "fr-FR", "fr", "FR", TitleType.Official, "Le Show", 2)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, ShowID, "en-GB", "en", "GB", TitleType.Official, "The Show UK", 3)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, RenamedShowID, "de", "de", "DE", TitleType.Official, "Die Show", 0)),
                (MetadataEntityType.Series, Row(MetadataEntityType.Series, RenamedShowID, "en-US", "en", "US", TitleType.Official, "Old Name", 1)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, EpisodeID, "ko", "ko", "KR", TitleType.Official, "진짜 제목", 1)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, EpisodeID, "zh-Hans", "zh", "CN", TitleType.Official, "第7集", 2)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, UnlistedEpisodeID, "ja", "ja", "JP", TitleType.Official, "本当", 0)),
                (MetadataEntityType.Episode, Row(MetadataEntityType.Episode, UnlistedEpisodeID, "fr", "fr", null, TitleType.Official, "Titre", 1)),
                (MetadataEntityType.Season, Row(MetadataEntityType.Season, SeasonID, "de", "de", "DE", TitleType.Official, "Staffel 1", 0)),
                (MetadataEntityType.Movie, Row(MetadataEntityType.Movie, MovieID, "fr-FR", "fr", "FR", TitleType.Official, "Le Film", 0)),
                (MetadataEntityType.Movie, Row(MetadataEntityType.Movie, MovieID, "ja", "ja", "JP", TitleType.Official, "映画", 1)),
                (MetadataEntityType.Collection, Row(MetadataEntityType.Collection, CollectionID, "de", "de", "DE", TitleType.Official, "Die Sammlung", 0)),
                (MetadataEntityType.Creator, Row(MetadataEntityType.Creator, PersonID, "unk", "unk", null, TitleType.Synonym, "First Alias", 0)),
                (MetadataEntityType.Creator, Row(MetadataEntityType.Creator, PersonID, "unk", "unk", null, TitleType.Synonym, "Second/Alias", 1)),
            };
            Assert.Equal(
                expectedTitles.OrderBy(row => MetadataNumberRegistry.GetNumber(row.Item1)).ThenBy(row => row.Item2.Split('|')[2], StringComparer.Ordinal).ThenBy(row => int.Parse(row.Item2.Split('|')[12])).Select(row => LanguageAgnostic(row.Item2)),
                titleRows.Select(LanguageAgnostic)
            );

            var overviewRows = ReadRows(connection, $"SELECT EntityType, EntityID, LanguageCode, CountryCode, Value, Ordering FROM Metadata_Overview WHERE EntitySource = {tmdb} AND EntityID IN ({string.Join(", ", _ids.Select(id => $"'{id}'"))}) ORDER BY EntityType, EntityID, Ordering")
                .Select(row => string.Join("|", row.Select(value => value is null ? "NULL" : Convert.ToString(value))))
                .ToList();
            var expectedOverviews = new[]
            {
                (MetadataEntityType.Series, $"{Kind(MetadataEntityType.Series)}|{ShowID}|de|DE|Eine Übersicht.|1"),
                (MetadataEntityType.Series, $"{Kind(MetadataEntityType.Series)}|{RenamedShowID}|ja|JP|概要|0"),
                (MetadataEntityType.Episode, $"{Kind(MetadataEntityType.Episode)}|{EpisodeID}|fr|FR|Un résumé.|0"),
                (MetadataEntityType.Episode, $"{Kind(MetadataEntityType.Episode)}|{UnlistedEpisodeID}|en|GB|British words.|0"),
                (MetadataEntityType.Movie, $"{Kind(MetadataEntityType.Movie)}|{MovieID}|en|US|Stale text.|0"),
                (MetadataEntityType.Creator, $"{Kind(MetadataEntityType.Creator)}|{PersonID}|ja|JP|経歴|0"),
            };
            Assert.Equal(
                expectedOverviews.OrderBy(row => MetadataNumberRegistry.GetNumber(row.Item1)).ThenBy(row => row.Item2.Split('|')[1], StringComparer.Ordinal).Select(row => row.Item2),
                overviewRows
            );

            // The flags say which entries listed their English text.
            string Flags(string table, string idColumn, int id, bool hasTitle)
                => string.Join("|", ReadRows(connection, $"SELECT {(hasTitle ? "EnglishTitleListed, " : string.Empty)}EnglishOverviewListed FROM {table} WHERE {idColumn} = {id}").Single().Select(value => Convert.ToInt32(value)));
            Assert.Equal("1|1", Flags("TMDB_Show", "TmdbShowID", ShowID, true));
            Assert.Equal("0|0", Flags("TMDB_Show", "TmdbShowID", RenamedShowID, true));
            Assert.Equal("1|0", Flags("TMDB_Season", "TmdbSeasonID", SeasonID, true));
        }
        finally
        {
            Cleanup(connection);
            ReleasedTextSchema.Drop(fixture, connection);
            ReleasedTmdbSchema.Drop(connection, restored);
        }
    }

    /// <summary>
    /// A copied row without its stored language.
    /// </summary>
    private static string LanguageAgnostic(string row)
    {
        var parts = row.Split('|');
        parts[4] = "*";
        return string.Join("|", parts);
    }

    #endregion
}
