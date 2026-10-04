using System;
using System.Data;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Runs the steps that store the English title and overview TMDB kept on its rows, twice, against
/// rows whose English text is TMDB's fallback to another language's, and checks what is stored.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbDefaultTextMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    private const int FallbackShowID = 9_870_301;

    private const int ListedShowID = 9_870_302;

    private const int CollectionID = 9_870_303;

    private const int SeasonID = 9_870_304;

    private const int CantoneseShowID = 9_870_305;

    private const int SilentShowID = 9_870_306;

    private const int UnlistedCollectionID = 9_870_307;

    private const int BehindShowID = 9_870_308;

    private const int ListedSpecialsID = 9_870_309;

    private const int NamedSeasonID = 9_870_310;

    private static readonly DateTime _created = new(2024, 1, 1, 10, 0, 0);

    private static int Tmdb
        => MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);

    private static int Kind(MetadataEntityType entityType)
        => MetadataNumberRegistry.GetNumber(entityType);

    private static void Seed(IDbConnection connection)
    {
        void Show(int id, string title, bool titleListed, string overview, bool overviewListed, string original, string language)
            => Insert(connection, "TMDB_Show", ("TmdbShowID", id), ("EnglishTitle", title), ("EnglishOverview", overview), ("OriginalTitle", original), ("OriginalLanguageCode", language),
                ("IsRestricted", false), ("Genres", string.Empty), ("ContentRatings", string.Empty), ("EpisodeCount", 0), ("SeasonCount", 0), ("AlternateOrderingCount", 0),
                ("UserRating", 0), ("UserVotes", 0), ("CreatedAt", _created), ("LastUpdatedAt", _created), ("EnglishTitleListed", titleListed), ("EnglishOverviewListed", overviewListed));
        void Text(string table, MetadataEntityType type, int id, string language, string code, string? country, string value, int ordering)
            => Insert(connection, table, [("EntitySource", Tmdb), ("EntityType", Kind(type)), ("EntityID", id.ToString()), ("Source", Tmdb), ("Language", language), ("LanguageCode", code),
                ("CountryCode", country), .. table is "Metadata_Title" ? [("TitleType", 2)] : Array.Empty<(string, object?)>(), ("Value", value), ("IsEnabled", true),
                ("Preference", 0), ("Ordering", ordering)]);

        // TMDB fell back to the original title, which it lists under its own language.
        Show(FallbackShowID, ".hack", false, "An MMO.", true, ".hack", "ja");
        Text("Metadata_Title", MetadataEntityType.Series, FallbackShowID, "ja", "ja", "JP", ".hack", 0);
        Text("Metadata_Title", MetadataEntityType.Series, FallbackShowID, "fr", "fr", "FR", ".hack Le Signe", 1);
        Text("Metadata_Overview", MetadataEntityType.Series, FallbackShowID, "de", "de", "DE", "Ein MMO.", 1);

        // The English title was listed; the overview on the row is not the English one TMDB lists.
        Show(ListedShowID, "Monogatari", true, "Although there are still traces.", false, "化物語", "ja");
        Text("Metadata_Title", MetadataEntityType.Series, ListedShowID, "ja", "ja", "JP", "化物語", 1);
        Text("Metadata_Overview", MetadataEntityType.Series, ListedShowID, "en-US", "en", "US", "Koyomi Araragi.", 0);
        Text("Metadata_Overview", MetadataEntityType.Series, ListedShowID, "ru", "ru", "RU", "Однажды.", 1);

        // A collection named in Korean, and a season TMDB only gave its generic name.
        Insert(connection, "TMDB_Collection", ("TmdbCollectionID", CollectionID), ("EnglishTitle", "로보트 태권V"), ("EnglishOverview", string.Empty), ("MovieCount", 0),
            ("CreatedAt", _created), ("LastUpdatedAt", _created));
        Text("Metadata_Title", MetadataEntityType.Collection, CollectionID, "ko", "ko", "KR", "로보트 태권V", 0);
        Insert(connection, "TMDB_Season", ("TmdbShowID", ListedShowID), ("TmdbSeasonID", SeasonID), ("EnglishTitle", "Season 1"), ("EnglishOverview", string.Empty), ("EpisodeCount", 0),
            ("SeasonNumber", 1), ("CreatedAt", _created), ("LastUpdatedAt", _created));

        // A season whose generic name TMDB listed as its English one, which is synthesized rather than stored.
        Insert(connection, "TMDB_Season", ("TmdbShowID", ListedShowID), ("TmdbSeasonID", ListedSpecialsID), ("EnglishTitle", "Specials"), ("EnglishOverview", string.Empty),
            ("EpisodeCount", 0), ("SeasonNumber", 0), ("CreatedAt", _created), ("LastUpdatedAt", _created), ("EnglishTitleListed", true));

        // TMDB's own codes for Cantonese and for no language.
        Show(CantoneseShowID, "Kung Fu Hustle", false, string.Empty, false, "功夫", "cn");
        Show(SilentShowID, "Silence", false, string.Empty, false, "Silence", "xx");

        // A fallback in no listed language, and an English title behind the English one TMDB listed.
        Insert(connection, "TMDB_Collection", ("TmdbCollectionID", UnlistedCollectionID), ("EnglishTitle", "ビックリマン"), ("EnglishOverview", string.Empty), ("MovieCount", 0),
            ("CreatedAt", _created), ("LastUpdatedAt", _created));
        Show(BehindShowID, "I Trust My Girlfriend!", false, string.Empty, false, "Ore wa Kanojo wo Shinjiteru!", "en");
        Text("Metadata_Title", MetadataEntityType.Series, BehindShowID, "en-US", "en", "US", "Ore wa Kanojo wo Shinjiteru!", 0);
    }

    #endregion

    #region Tests

    [Fact]
    public void OnlyEnglishTextIsStoredAsEnglish()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var restored = ReleasedTmdbSchema.Restore(connection, fixture.Backend);
        try
        {
            Seed(connection);
            for (var run = 0; run < 2; run++)
            {
                Run(DatabaseFixes.CopyTmdbDefaultTitles, connection);
                Run(DatabaseFixes.CopyTmdbDefaultOverviews, connection);
            }

            string Rows(string table, string columns)
                => string.Join("; ", Read(connection, $"SELECT EntityID, {columns}, Value, Ordering FROM {table} WHERE EntitySource = {Tmdb} ORDER BY EntityID, Ordering"));
            Assert.Equal(
                string.Join("; ",
                    $"{FallbackShowID}|ja|ja|1|.hack|0", $"{FallbackShowID}|fr|fr|2|.hack Le Signe|1",
                    $"{ListedShowID}|en-US|en|1|Monogatari|0", $"{ListedShowID}|ja|ja|2|化物語|1",
                    $"{CollectionID}|ko|ko|1|로보트 태권V|0",
                    $"{CantoneseShowID}|en-US|en|1|Kung Fu Hustle|0", $"{CantoneseShowID}|zh|cn|2|功夫|1",
                    $"{SilentShowID}|none|xx|1|Silence|0",
                    $"{UnlistedCollectionID}|unk|unk|1|ビックリマン|0",
                    $"{BehindShowID}|en-US|en|2|Ore wa Kanojo wo Shinjiteru!|0", $"{BehindShowID}|en-US|en|1|I Trust My Girlfriend!|1"),
                Rows("Metadata_Title", "Language, LanguageCode, TitleType"));
            Assert.Equal(
                string.Join("; ",
                    $"{FallbackShowID}|en-US|An MMO.|0", $"{FallbackShowID}|de|Ein MMO.|1",
                    $"{ListedShowID}|en-US|Koyomi Araragi.|0", $"{ListedShowID}|ru|Однажды.|1"),
                Rows("Metadata_Overview", "Language"));
        }
        finally
        {
            TmdbDataMigrationTests.Cleanup(connection, restored, fixture.Backend);
        }
    }

    [Fact]
    public void TheGenericEnglishSeasonNamesStoredBeforeAreRemoved()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        try
        {
            void Season(int id, int number)
                => Insert(connection, "Metadata_Season", ("Source", Tmdb), ("ProviderID", id.ToString()), ("SeriesID", ListedShowID.ToString()), ("SeasonNumber", number),
                    ("CreatedAt", _created), ("LastUpdatedAt", _created));
            void Title(int id, string language, string code, string? country, int type, string value, int ordering)
                => Insert(connection, "Metadata_Title", ("EntitySource", Tmdb), ("EntityType", Kind(MetadataEntityType.Season)), ("EntityID", id.ToString()), ("Source", Tmdb),
                    ("Language", language), ("LanguageCode", code), ("CountryCode", country), ("TitleType", type), ("Value", value), ("IsEnabled", true), ("Preference", 0),
                    ("Ordering", ordering));

            Season(ListedSpecialsID, 0);
            Title(ListedSpecialsID, "en-US", "en", "US", 1, "Specials", 0);
            Title(ListedSpecialsID, "fr", "fr", "FR", 2, "Specials", 1);
            Title(ListedSpecialsID, "en-GB", "en", "GB", 2, "Season 0", 2);
            Season(NamedSeasonID, 2);
            Title(NamedSeasonID, "en-US", "en", "US", 1, "The Arc", 0);
            Title(NamedSeasonID, "en-GB", "en", "GB", 2, "season 2", 1);
            Title(NamedSeasonID, "de", "de", "DE", 2, "Staffel 2", 2);
            for (var run = 0; run < 2; run++)
                Run(DatabaseFixes.RemoveTmdbGenericSeasonTitles, connection);

            // Only English generic names go; a season left without a main title is named by the core.
            Assert.Equal(
                [$"{ListedSpecialsID}|fr|Specials", $"{NamedSeasonID}|en|The Arc", $"{NamedSeasonID}|de|Staffel 2"],
                Read(connection, $"SELECT EntityID, LanguageCode, Value FROM Metadata_Title WHERE EntitySource = {Tmdb} ORDER BY EntityID, Ordering"));
        }
        finally
        {
            Execute(connection, $"DELETE FROM Metadata_Title WHERE EntitySource = {Tmdb}");
            Execute(connection, $"DELETE FROM Metadata_Season WHERE Source = {Tmdb}");
        }
    }

    #endregion
}
