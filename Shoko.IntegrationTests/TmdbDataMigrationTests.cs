using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.Databases;
using Xunit;
using static Shoko.IntegrationTests.Sql;

namespace Shoko.IntegrationTests;

/// <summary>
/// Seeds TMDB's released tables, runs the steps that copy them into the shared metadata tables,
/// twice as an interrupted step is run again, and checks every copied row.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class TmdbDataMigrationTests(DatabaseMigrationFixture fixture)
{
    #region Fixture Data

    internal const int ShowID = 9_870_101;

    internal const int FreshShowID = 9_870_102;

    internal const int SeasonID = 9_870_111;

    internal const int SpecialsID = 9_870_112;

    internal const int FirstEpisodeID = 9_870_121;

    internal const int SecondEpisodeID = 9_870_122;

    internal const int SpecialID = 9_870_123;

    internal const int AliceID = 9_870_131;

    internal const int BobID = 9_870_132;

    internal const int UnknownPersonID = 9_870_133;

    internal const int MovieID = 9_870_141;

    internal const int MemberMovieID = 9_870_142;

    internal const int CollectionID = 9_870_151;

    internal const int StudioID = 9_870_161;

    internal const int UnknownStudioID = 9_870_162;

    internal const int NetworkID = 9_870_171;

    internal const int UnknownNetworkID = 9_870_172;

    internal const string OrderingID = "aa00000000000000000000a1";

    /// <summary>
    /// The show's two posters, the one on the row second, with the local IDs they are seeded under.
    /// </summary>
    internal static readonly (int LocalID, string ResourceID)[] Posters = [(9_870_901, "p1.jpg"), (9_870_902, "p2.jpg")];

    /// <summary>
    /// Every copy step, in the order the migration runs them.
    /// </summary>
    internal static readonly Func<object, Tuple<bool, string?>>[] Steps =
    [
        DatabaseFixes.CopyTmdbShows,
        DatabaseFixes.CopyTmdbSeasons,
        DatabaseFixes.CopyTmdbEpisodes,
        DatabaseFixes.CopyTmdbMovies,
        DatabaseFixes.CopyTmdbCollections,
        DatabaseFixes.CopyTmdbCollectionMovies,
        DatabaseFixes.CopyTmdbContentRatings,
        DatabaseFixes.CopyTmdbTags,
        DatabaseFixes.CopyTmdbPeople,
        DatabaseFixes.CopyTmdbCompanies,
        DatabaseFixes.CopyTmdbCompanyLinks,
        DatabaseFixes.CopyTmdbNetworks,
        DatabaseFixes.CopyTmdbShowNetworks,
        DatabaseFixes.CopyTmdbEpisodeCast,
        DatabaseFixes.CopyTmdbEpisodeCrew,
        DatabaseFixes.CopyTmdbMovieCast,
        DatabaseFixes.CopyTmdbMovieCrew,
        DatabaseFixes.AggregateTmdbShowCast,
        DatabaseFixes.AggregateTmdbShowCrew,
        DatabaseFixes.CopyTmdbOrderings,
        DatabaseFixes.CopyTmdbOrderingGroups,
        DatabaseFixes.CopyTmdbOrderingEpisodes,
        DatabaseFixes.CopyTmdbOrderingNetworks,
        DatabaseFixes.CopyTmdbSuggestions,
        DatabaseFixes.CopyTmdbDefaultTitles,
        DatabaseFixes.CopyTmdbDefaultOverviews,
        DatabaseFixes.RewriteTmdbDefaultOrderingIDs,
        DatabaseFixes.FillTmdbEpisodeLinkNumbers,
        DatabaseFixes.CopyTmdbDefaultImages,
    ];

    /// <summary>
    /// The shared tables the copy steps write, with the column naming their source.
    /// </summary>
    private static readonly (string Table, string SourceColumn)[] _targets =
    [
        ("Metadata_Series", "Source"),
        ("Metadata_Season", "Source"),
        ("Metadata_Episode", "Source"),
        ("Metadata_Movie", "Source"),
        ("Metadata_Collection", "Source"),
        ("Metadata_Collection_Member", "Source"),
        ("Metadata_ContentRating", "Source"),
        ("Metadata_Tag_Entry", "Source"),
        ("Metadata_Tag", "Source"),
        ("Metadata_Creator", "Source"),
        ("Metadata_Studio_Entry", "Source"),
        ("Metadata_Studio", "Source"),
        ("Metadata_Network_Entry", "Source"),
        ("Metadata_Network", "Source"),
        ("Metadata_Cast", "Source"),
        ("Metadata_Crew", "Source"),
        ("Metadata_Ordering_Entry", "Source"),
        ("Metadata_Ordering_Group", "Source"),
        ("Metadata_Ordering", "Source"),
        ("Metadata_Suggestion", "Source"),
        ("Metadata_Title", "EntitySource"),
        ("Metadata_Overview", "EntitySource"),
    ];

    #endregion

    #region Helpers

    private static readonly DateTime _created = new(2024, 1, 1, 10, 0, 0);

    private static readonly DateTime _updated = new(2024, 2, 1, 10, 0, 0);

    private static string Kind(MetadataEntityType entityType)
        => MetadataNumberRegistry.GetNumber(entityType).ToString();

    private static int Tmdb
        => MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);

    internal static void Seed(IDbConnection connection)
    {
        Insert(connection, "TMDB_Show", ("TmdbShowID", ShowID), ("EnglishTitle", "A Show"), ("EnglishOverview", "About it."), ("OriginalTitle", "番組"), ("OriginalLanguageCode", "ja"),
            ("IsRestricted", false), ("Genres", "Animation|||Sci-Fi"), ("Keywords", "school|||magic"), ("ContentRatings", "US,TV-14|BR,12|BR,14"),
            ("ProductionCountries", "JP,Japan|KR,Korea, Republic of"), ("EpisodeCount", 2), ("SeasonCount", 1), ("AlternateOrderingCount", 1), ("UserRating", 8), ("UserVotes", 10),
            ("FirstAiredAt", new DateTime(2020, 1, 2)), ("LastAiredAt", new DateTime(2020, 6, 30)), ("CreatedAt", _created), ("LastUpdatedAt", _updated), ("TvdbShowID", 555),
            ("PosterPath", "/p2.jpg"), ("BackdropPath", "/b1.jpg"), ("PreferredOrderingID", $"tmdb://ordering/{ShowID}"));
        Insert(connection, "TMDB_Show", ("TmdbShowID", FreshShowID), ("EnglishTitle", "Second"), ("EnglishOverview", string.Empty), ("OriginalTitle", "Second"), ("OriginalLanguageCode", "en"),
            ("IsRestricted", true), ("Genres", string.Empty), ("Keywords", null), ("ContentRatings", string.Empty), ("ProductionCountries", null), ("EpisodeCount", 0), ("SeasonCount", 0),
            ("AlternateOrderingCount", 0), ("UserRating", 0), ("UserVotes", 0), ("FirstAiredAt", null), ("LastAiredAt", null), ("CreatedAt", _created), ("LastUpdatedAt", _created),
            ("TvdbShowID", 0), ("PosterPath", null), ("BackdropPath", null), ("PreferredOrderingID", $"tmdb://ordering/{OrderingID}"));

        Insert(connection, "TMDB_Season", ("TmdbShowID", ShowID), ("TmdbSeasonID", SeasonID), ("EnglishTitle", "Season 1"), ("EnglishOverview", string.Empty), ("EpisodeCount", 2),
            ("SeasonNumber", 1), ("CreatedAt", _created), ("LastUpdatedAt", _updated), ("PosterPath", null));
        Insert(connection, "TMDB_Season", ("TmdbShowID", ShowID), ("TmdbSeasonID", SpecialsID), ("EnglishTitle", "Specials"), ("EnglishOverview", string.Empty), ("EpisodeCount", 1),
            ("SeasonNumber", 0), ("CreatedAt", _created), ("LastUpdatedAt", _updated), ("PosterPath", null));

        void Episode(int id, int seasonID, int season, int number, string title, int? runtime, int? tvdbID, bool hidden)
            => Insert(connection, "TMDB_Episode", ("TmdbShowID", ShowID), ("TmdbSeasonID", seasonID), ("TmdbEpisodeID", id), ("EnglishTitle", title), ("EnglishOverview", $"{title}."),
                ("SeasonNumber", season), ("EpisodeNumber", number), ("Runtime", runtime), ("UserRating", 7), ("UserVotes", 3), ("AiredAt", new DateTime(2020, 1, number + 1)),
                ("CreatedAt", _created), ("LastUpdatedAt", _updated), ("TvdbEpisodeID", tvdbID), ("IsHidden", hidden), ("ThumbnailPath", null));
        Episode(FirstEpisodeID, SeasonID, 1, 1, "Episode 1", 24, null, false);
        Episode(SecondEpisodeID, SeasonID, 1, 2, "Second Title", null, null, true);
        Episode(SpecialID, SpecialsID, 0, 1, "The Special", 12, 777, false);

        void Cast(int episodeID, int seasonID, int personID, string character, bool guest, int ordering)
            => Insert(connection, "TMDB_Episode_Cast", ("TmdbShowID", ShowID), ("TmdbSeasonID", seasonID), ("TmdbEpisodeID", episodeID), ("TmdbPersonID", personID),
                ("TmdbCreditID", $"c{episodeID}-{personID}"), ("CharacterName", character), ("IsGuestRole", guest), ("Ordering", ordering));
        Cast(FirstEpisodeID, SeasonID, BobID, "Villain", true, 1);
        Cast(FirstEpisodeID, SeasonID, AliceID, "Hero", false, 0);
        Cast(SecondEpisodeID, SeasonID, AliceID, "Hero", false, 0);
        Cast(SecondEpisodeID, SeasonID, UnknownPersonID, "Extra", false, 1);
        Cast(SpecialID, SpecialsID, BobID, "Villain", true, 0);

        void Crew(int episodeID, int personID, string department, string job)
            => Insert(connection, "TMDB_Episode_Crew", ("TmdbShowID", ShowID), ("TmdbSeasonID", SeasonID), ("TmdbEpisodeID", episodeID), ("TmdbPersonID", personID),
                ("TmdbCreditID", $"k{episodeID}-{personID}"), ("Job", job), ("Department", department));
        Crew(FirstEpisodeID, BobID, "Writing", "Writer");
        Crew(FirstEpisodeID, AliceID, "Directing", "Director");
        Crew(SecondEpisodeID, AliceID, "Directing", "Director");

        Insert(connection, "TMDB_Movie", ("TmdbMovieID", MovieID), ("TmdbCollectionID", CollectionID), ("EnglishTitle", "A Movie"), ("EnglishOverview", "A film."),
            ("OriginalTitle", "映画"), ("OriginalLanguageCode", "ja"), ("IsRestricted", false), ("IsVideo", true), ("Genres", "Drama"), ("Keywords", null), ("ContentRatings", "JP,G"),
            ("ProductionCountries", "JP,Japan"), ("Runtime", 95), ("UserRating", 6), ("UserVotes", 2), ("ReleasedAt", new DateTime(2021, 3, 4)), ("CreatedAt", _created),
            ("LastUpdatedAt", _created), ("ImdbMovieID", "tt0000001"), ("PosterPath", null), ("BackdropPath", null));
        Insert(connection, "TMDB_Movie", ("TmdbMovieID", MemberMovieID), ("TmdbCollectionID", null), ("EnglishTitle", "Another Movie"), ("EnglishOverview", string.Empty),
            ("OriginalTitle", "Another Movie"), ("OriginalLanguageCode", "en"), ("IsRestricted", false), ("IsVideo", false), ("Genres", string.Empty), ("Keywords", null),
            ("ContentRatings", string.Empty), ("ProductionCountries", null), ("Runtime", null), ("UserRating", 0), ("UserVotes", 0), ("ReleasedAt", null), ("CreatedAt", _created),
            ("LastUpdatedAt", _updated), ("ImdbMovieID", null), ("PosterPath", null), ("BackdropPath", null));
        Insert(connection, "TMDB_Movie_Cast", ("TmdbMovieID", MovieID), ("TmdbPersonID", AliceID), ("TmdbCreditID", "m1"), ("CharacterName", "Lead"), ("Ordering", 0));
        Insert(connection, "TMDB_Movie_Crew", ("TmdbMovieID", MovieID), ("TmdbPersonID", BobID), ("TmdbCreditID", "m2"), ("Job", "Screenplay"), ("Department", "Writing"));
        Insert(connection, "TMDB_Collection", ("TmdbCollectionID", CollectionID), ("EnglishTitle", "A Collection"), ("EnglishOverview", "Both."), ("MovieCount", 2),
            ("CreatedAt", _created), ("LastUpdatedAt", _updated));
        Insert(connection, "TMDB_Collection_Movie", ("TmdbCollectionID", CollectionID), ("TmdbMovieID", MemberMovieID), ("Ordering", 0));

        Insert(connection, "TMDB_Person", ("TmdbPersonID", AliceID), ("EnglishName", "Alice"), ("EnglishBiography", "A life."), ("Gender", 1), ("IsRestricted", false),
            ("BirthDay", new DateTime(1980, 5, 6)), ("DeathDay", null), ("PlaceOfBirth", "Tokyo"), ("CreatedAt", _created), ("LastUpdatedAt", _updated), ("LastOrphanedAt", null),
            ("ImdbPersonID", "nm0000001"));
        Insert(connection, "TMDB_Person", ("TmdbPersonID", BobID), ("EnglishName", "Bob"), ("EnglishBiography", string.Empty), ("Gender", 2), ("IsRestricted", true),
            ("BirthDay", null), ("DeathDay", null), ("PlaceOfBirth", null), ("CreatedAt", _created), ("LastUpdatedAt", _created), ("LastOrphanedAt", null), ("ImdbPersonID", null));

        Insert(connection, "TMDB_Company", ("TmdbCompanyID", StudioID), ("Name", "Studio"), ("CountryOfOrigin", "JP"));
        Insert(connection, "TMDB_Company_Entity", ("TmdbCompanyID", UnknownStudioID), ("TmdbEntityType", 4), ("TmdbEntityID", ShowID), ("Ordering", 1));
        Insert(connection, "TMDB_Company_Entity", ("TmdbCompanyID", StudioID), ("TmdbEntityType", 4), ("TmdbEntityID", ShowID), ("Ordering", 0));
        Insert(connection, "TMDB_Company_Entity", ("TmdbCompanyID", StudioID), ("TmdbEntityType", 2), ("TmdbEntityID", MovieID), ("Ordering", 0));
        Insert(connection, "TMDB_Network", ("TmdbNetworkID", NetworkID), ("Name", "Net"), ("CountryOfOrigin", string.Empty), ("LastOrphanedAt", null));
        Insert(connection, "TMDB_Show_Network", ("TmdbShowID", ShowID), ("TmdbNetworkID", NetworkID), ("Ordering", 0));

        Insert(connection, "TMDB_AlternateOrdering", ("TmdbShowID", ShowID), ("TmdbNetworkID", UnknownNetworkID), ("TmdbEpisodeGroupCollectionID", OrderingID),
            ("EnglishTitle", "Absolute"), ("EnglishOverview", string.Empty), ("EpisodeCount", 3), ("SeasonCount", 2), ("Type", 2), ("CreatedAt", _created), ("LastUpdatedAt", _updated));
        void Group(string groupID, int number, string name)
            => Insert(connection, "TMDB_AlternateOrdering_Season", ("TmdbShowID", ShowID), ("TmdbEpisodeGroupCollectionID", OrderingID), ("TmdbEpisodeGroupID", groupID),
                ("EnglishTitle", name), ("SeasonNumber", number), ("EpisodeCount", 1), ("IsLocked", true), ("CreatedAt", _created), ("LastUpdatedAt", _updated));
        Group("bb00000000000000000000b1", 1, "Part A");
        Group("bb00000000000000000000b0", 0, "Extras");
        Group("bb00000000000000000000b2", 0, "More");
        void Place(string groupID, int episodeID, int number)
            => Insert(connection, "TMDB_AlternateOrdering_Episode", ("TmdbShowID", ShowID), ("TmdbEpisodeGroupCollectionID", OrderingID), ("TmdbEpisodeGroupID", groupID),
                ("TmdbEpisodeID", episodeID), ("SeasonNumber", 1), ("EpisodeNumber", number), ("CreatedAt", _created), ("LastUpdatedAt", _updated));
        Place("bb00000000000000000000b1", FirstEpisodeID, 2);
        Place("bb00000000000000000000b1", SecondEpisodeID, 1);
        Place("bb00000000000000000000b0", SpecialID, 1);
        Place("bb00000000000000000000b0", 9_870_199, 2);

        var series = MetadataNumberRegistry.GetNumber(MetadataEntityType.Series);
        Insert(connection, "TMDB_Suggestion", ("TmdbEntityType", series), ("TmdbEntityID", ShowID), ("SuggestedTmdbEntityID", 4242), ("Kind", 1), ("Ordering", 0));
        Insert(connection, "TMDB_Suggestion", ("TmdbEntityType", series), ("TmdbEntityID", ShowID), ("SuggestedTmdbEntityID", FreshShowID), ("Kind", 0), ("Ordering", 0));

        // What the earlier text copy left: TMDB's own list, without the English text on the row.
        void Title(MetadataEntityType type, int id, string language, string code, string? country, string value, int ordering)
            => Insert(connection, "Metadata_Title", ("EntitySource", Tmdb), ("EntityType", (int)MetadataNumberRegistry.GetNumber(type)), ("EntityID", id.ToString()), ("Source", Tmdb),
                ("Language", language), ("LanguageCode", code), ("CountryCode", country), ("TitleType", 2), ("Value", value), ("IsEnabled", true), ("Preference", 0), ("Ordering", ordering));
        Title(MetadataEntityType.Series, ShowID, "ja", "ja", "JP", "番組", 0);
        Title(MetadataEntityType.Series, ShowID, "fr-FR", "fr", "FR", "Le Show", 2);
        Title(MetadataEntityType.Series, FreshShowID, "de", "de", "DE", "Die Zweite", 0);
        // Two posters, the one on the row second, under the IDs their source and resource IDs give.
        foreach (var (ordering, (localID, resourceID)) in Posters.Index())
        {
            var imageID = IImageManager.GetIDForImageSourceAndResourceID(MetadataSource.TMDB, resourceID).ToString().ToUpperInvariant();
            Insert(connection, "ShokoImage", ("ID", imageID), ("LocalID", localID), ("PrimaryID", imageID), ("ResourceID", resourceID), ("ContentType", "image/jpeg"), ("Source", Tmdb),
                ("CreatedAt", _created), ("LastUpdatedAt", _created));
            Insert(connection, "ShokoImage_Entity", ("ImageID", imageID), ("PrimaryImageID", imageID), ("ImageType", 1), ("ImageSource", Tmdb), ("EntitySource", Tmdb),
                ("EntityType", (int)series), ("EntityID", ShowID.ToString()), ("IsEnabled", true), ("IsDesired", true), ("IsPreferred", false), ("Ordering", ordering), ("Source", Tmdb),
                ("CreatedAt", _created), ("LastUpdatedAt", _created));
        }

        Insert(connection, "Metadata_Overview", ("EntitySource", Tmdb), ("EntityType", (int)series), ("EntityID", ShowID.ToString()), ("Source", Tmdb), ("Language", "de"),
            ("LanguageCode", "de"), ("CountryCode", "DE"), ("Value", "Darüber."), ("IsEnabled", true), ("Preference", 0), ("Ordering", 0));

        // A link to an episode, kept without its season and numbers while TMDB was a core source.
        Insert(connection, "CrossRef_AniDB_Metadata_Episode", ("Source", Tmdb), ("AnidbAnimeID", 9_870_001), ("AnidbEpisodeID", 9_870_002), ("ProviderID", SpecialID.ToString()),
            ("ProviderParentID", ShowID.ToString()), ("MatchRating", 1), ("Ordering", 0));
    }

    internal static void Cleanup(IDbConnection connection, IReadOnlyList<string> restored, string backend)
    {
        foreach (var (table, column) in _targets)
            Execute(connection, $"DELETE FROM {table} WHERE {column} = {Tmdb}");
        Execute(connection, $"DELETE FROM ShokoImage_Entity WHERE EntitySource = {Tmdb} AND EntityID = '{ShowID}'");
        Execute(connection, "DELETE FROM ShokoImage WHERE LocalID IN (9870901, 9870902)");
        Execute(connection, $"DELETE FROM CrossRef_AniDB_Metadata_Episode WHERE Source = {Tmdb} AND AnidbAnimeID = 9870001");

        foreach (var table in ReleasedTmdbSchema.Present(connection, backend).Except(restored))
            Execute(connection, $"DELETE FROM {table}");
        ReleasedTmdbSchema.Drop(connection, restored);
    }

    #endregion

    #region Tests

    [Fact]
    public void TheDropStepsLeaveNoTmdbTable()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        Assert.Empty(ReleasedTmdbSchema.Present(connection, fixture.Backend));
    }

    [Fact]
    public void TheCopyStepsMoveEveryTmdbRowIntoTheSharedTables()
    {
        Assert.True(fixture.Success, fixture.FailureMessage);

        using var connection = fixture.OpenConnection();
        var restored = ReleasedTmdbSchema.Restore(connection, fixture.Backend);
        try
        {
            Seed(connection);
            for (var run = 0; run < 2; run++)
                foreach (var step in Steps)
                    Run(step, connection);

            var tmdb = Tmdb;
            string Where(string column = "Source") => $"WHERE {column} = {tmdb}";

            // Entries keep TMDB's IDs and pin the images TMDB named; a show never updated was never refreshed.
            Assert.Equal(
                [
                    $"{ShowID}|{(int)Shoko.Abstractions.Metadata.Enums.AnimeType.TV}|2020-01-02|2020-06-30|8|10|0|ja|[\"tvdb://series/555\"]|tmdb://ordering/default/{ShowID}|{{\"ProductionCountries\":[\"JP\",\"KR\"],\"PrimaryResourceID\":\"p2.jpg\",\"BackdropResourceID\":\"b1.jpg\"}}|1",
                    $"{FreshShowID}|{(int)Shoko.Abstractions.Metadata.Enums.AnimeType.TV}|NULL|NULL|0|0|1|en|NULL|tmdb://ordering/{OrderingID}|NULL|0",
                ],
                Read(connection, $"SELECT ProviderID, Type, AirDate, EndDate, Rating, RatingVotes, IsRestricted, OriginalLanguageCode, CrossSourceIDs, PreferredOrderingID, ExtraData, " +
                    $"CASE WHEN LastRefreshedAt IS NULL THEN 0 ELSE 1 END FROM Metadata_Series {Where()} ORDER BY ProviderID"));
            Assert.Equal([$"{SeasonID}|{ShowID}|1", $"{SpecialsID}|{ShowID}|0"], Read(connection, $"SELECT ProviderID, SeriesID, SeasonNumber FROM Metadata_Season {Where()} ORDER BY ProviderID"));
            Assert.Equal(
                [
                    $"{FirstEpisodeID}|{ShowID}|{SeasonID}|1|1|1|1440|NULL|0",
                    $"{SecondEpisodeID}|{ShowID}|{SeasonID}|1|2|1|0|NULL|1",
                    $"{SpecialID}|{ShowID}|{SpecialsID}|0|1|3|720|[\"tvdb://episode/777\"]|0",
                ],
                Read(connection, $"SELECT ProviderID, SeriesID, SeasonID, SeasonNumber, EpisodeNumber, Type, Runtime, CrossSourceIDs, IsHidden FROM Metadata_Episode {Where()} ORDER BY ProviderID"));
            Assert.Equal(
                [$"{MovieID}|1|5700|[\"imdb://movie/tt0000001\"]|{{\"ProductionCountries\":[\"JP\"]}}|0", $"{MemberMovieID}|0|NULL|NULL|NULL|1"],
                Read(connection, $"SELECT ProviderID, IsVideo, Runtime, CrossSourceIDs, ExtraData, CASE WHEN LastRefreshedAt IS NULL THEN 0 ELSE 1 END FROM Metadata_Movie {Where()} ORDER BY ProviderID"));
            Assert.Equal([$"{CollectionID}"], Read(connection, $"SELECT ProviderID FROM Metadata_Collection {Where()}"));
            Assert.Equal(
                [$"{CollectionID}|{Kind(MetadataEntityType.Movie)}|{MemberMovieID}|0", $"{CollectionID}|{Kind(MetadataEntityType.Movie)}|{MovieID}|1"],
                Read(connection, $"SELECT CollectionID, MemberType, MemberID, Ordering FROM Metadata_Collection_Member {Where()} ORDER BY Ordering"));

            // Every content rating, a country rated twice keeping both.
            Assert.Equal(
                [
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|US|TV-14|0",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|BR|12|1",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|BR|14|2",
                    $"{Kind(MetadataEntityType.Movie)}|{MovieID}|JP|G|0",
                ],
                Read(connection, $"SELECT EntityType, EntityID, CountryCode, Rating, Ordering FROM Metadata_ContentRating {Where()} ORDER BY EntityType, Ordering"));

            // Provisional tags, genres first.
            Assert.Equal(
                [
                    $"{Kind(MetadataEntityType.Movie)}|{MovieID}|genre/Drama|Drama|1|0",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|genre/Animation|Animation|1|0",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|genre/Sci-Fi|Sci-Fi|1|1",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|keyword/school|school|2|2",
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|keyword/magic|magic|2|3",
                ],
                Read(connection, $"SELECT e.EntityType, e.EntityID, t.ProviderID, t.Name, t.Kind, e.Ordering FROM Metadata_Tag_Entry e INNER JOIN Metadata_Tag t ON t.Metadata_TagID = e.TagID " +
                    $"WHERE e.Source = {tmdb} ORDER BY e.EntityType DESC, e.Ordering"));

            // People, with a stub for the one a credit names that TMDB's table did not hold.
            Assert.Equal(
                [
                    $"{AliceID}|Alice|A life.|1|1980-05-06|0|{{\"PlaceOfBirth\":\"Tokyo\"}}|1|1",
                    $"{BobID}|Bob|NULL|2|NULL|1|NULL|0|1",
                    $"{UnknownPersonID}||NULL|0|NULL|0|NULL|0|0",
                ],
                Read(connection, $"SELECT ProviderID, Name, Description, Gender, BirthDay, IsRestricted, ExtraData, CASE WHEN Resources LIKE '%nm0000001%' THEN 1 ELSE 0 END, " +
                    $"CASE WHEN LastUpdatedAt IS NULL THEN 0 ELSE 1 END FROM Metadata_Creator {Where()} ORDER BY ProviderID"));
            // TMDB's people keep their creation date; the stub is created when copied.
            Assert.Equal([$"{AliceID}|copied", $"{BobID}|copied", $"{UnknownPersonID}|now"],
                Read(connection, "SELECT ProviderID, CASE WHEN CreatedAt > '2024-01-01 09:00:00' AND CreatedAt < '2024-01-01 11:00:00' THEN 'copied' " +
                    $"WHEN CreatedAt > '2025-01-01 00:00:00' THEN 'now' ELSE 'other' END FROM Metadata_Creator {Where()} ORDER BY ProviderID"));
            Assert.Equal([$"{StudioID}|Studio|JP|1", $"{UnknownStudioID}||NULL|0"],
                Read(connection, $"SELECT ProviderID, Name, CountryOfOrigin, CASE WHEN LastUpdatedAt IS NULL THEN 0 ELSE 1 END FROM Metadata_Studio {Where()} ORDER BY ProviderID"));
            Assert.Equal(
                [$"{Kind(MetadataEntityType.Series)}|{ShowID}|{StudioID}|0", $"{Kind(MetadataEntityType.Series)}|{ShowID}|{UnknownStudioID}|1", $"{Kind(MetadataEntityType.Movie)}|{MovieID}|{StudioID}|0"],
                Read(connection, $"SELECT e.EntityType, e.EntityID, s.ProviderID, e.Ordering FROM Metadata_Studio_Entry e INNER JOIN Metadata_Studio s ON s.Metadata_StudioID = e.StudioID " +
                    $"WHERE e.Source = {tmdb} ORDER BY e.EntityType, e.Ordering"));
            Assert.Equal(
                [$"{Kind(MetadataEntityType.Series)}|{ShowID}|{NetworkID}|Net", $"{Kind(MetadataEntityType.Ordering)}|{OrderingID}|{UnknownNetworkID}|"],
                Read(connection, $"SELECT e.EntityType, e.EntityID, n.ProviderID, n.Name FROM Metadata_Network_Entry e INNER JOIN Metadata_Network n ON n.Metadata_NetworkID = e.NetworkID " +
                    $"WHERE e.Source = {tmdb} ORDER BY e.EntityType"));

            // Credits, with the show's and seasons' gathered from the episodes' in the order TMDB's models gave them.
            string Credits(string table, string name)
                => string.Join("; ", Read(connection, $"SELECT e.EntityType, e.EntityID, c.ProviderID, e.{name}, e.Ordering FROM {table} e INNER JOIN Metadata_Creator c ON c.Metadata_CreatorID = e.CreatorID " +
                    $"WHERE e.Source = {tmdb} ORDER BY e.EntityType, e.EntityID, e.Ordering"));
            string Credit(MetadataEntityType type, int id, int personID, string name, int ordering) => $"{Kind(type)}|{id}|{personID}|{name}|{ordering}";
            Assert.Equal(
                string.Join("; ",
                    Credit(MetadataEntityType.Series, ShowID, AliceID, "Hero", 0), Credit(MetadataEntityType.Series, ShowID, BobID, "Villain", 1), Credit(MetadataEntityType.Series, ShowID, UnknownPersonID, "Extra", 2),
                    Credit(MetadataEntityType.Season, SeasonID, AliceID, "Hero", 0), Credit(MetadataEntityType.Season, SeasonID, BobID, "Villain", 1), Credit(MetadataEntityType.Season, SeasonID, UnknownPersonID, "Extra", 2),
                    Credit(MetadataEntityType.Season, SpecialsID, BobID, "Villain", 0),
                    Credit(MetadataEntityType.Episode, FirstEpisodeID, AliceID, "Hero", 0), Credit(MetadataEntityType.Episode, FirstEpisodeID, BobID, "Villain", 1),
                    Credit(MetadataEntityType.Episode, SecondEpisodeID, AliceID, "Hero", 0), Credit(MetadataEntityType.Episode, SecondEpisodeID, UnknownPersonID, "Extra", 1),
                    Credit(MetadataEntityType.Episode, SpecialID, BobID, "Villain", 0),
                    Credit(MetadataEntityType.Movie, MovieID, AliceID, "Lead", 0)),
                Credits("Metadata_Cast", "Name"));
            Assert.Equal(
                string.Join("; ",
                    Credit(MetadataEntityType.Series, ShowID, AliceID, "Directing, Director", 0), Credit(MetadataEntityType.Series, ShowID, BobID, "Writing, Writer", 1),
                    Credit(MetadataEntityType.Season, SeasonID, AliceID, "Directing, Director", 0), Credit(MetadataEntityType.Season, SeasonID, BobID, "Writing, Writer", 1),
                    Credit(MetadataEntityType.Episode, FirstEpisodeID, AliceID, "Directing, Director", 0), Credit(MetadataEntityType.Episode, FirstEpisodeID, BobID, "Writing, Writer", 1),
                    Credit(MetadataEntityType.Episode, SecondEpisodeID, AliceID, "Directing, Director", 0),
                    Credit(MetadataEntityType.Movie, MovieID, BobID, "Writing, Screenplay", 0)),
                Credits("Metadata_Crew", "Name"));
            Assert.Equal(["ja"], Read(connection, $"SELECT DISTINCT LanguageCode FROM Metadata_Cast {Where()}"));
            Assert.Equal(["Villain|Guest star"], Read(connection, $"SELECT DISTINCT Name, RoleNotes FROM Metadata_Cast {Where()} AND RoleNotes IS NOT NULL"));
            Assert.Equal(
                [$"Directing, Director|{(int)Shoko.Abstractions.Metadata.Enums.CrewRoleType.Director}", $"Writing, Screenplay|{(int)Shoko.Abstractions.Metadata.Enums.CrewRoleType.None}", $"Writing, Writer|{(int)Shoko.Abstractions.Metadata.Enums.CrewRoleType.None}"],
                Read(connection, $"SELECT DISTINCT Name, RoleType FROM Metadata_Crew {Where()} ORDER BY Name"));

            // The alternate ordering, its groups in order with one specials group, and its places.
            Assert.Equal([$"{OrderingID}|{tmdb}|{ShowID}|2|Absolute|NULL"], Read(connection, $"SELECT ProviderID, SeriesSource, SeriesID, Type, Name, Description FROM Metadata_Ordering {Where()}"));
            Assert.Equal(
                ["bb00000000000000000000b0|0|Extras|1", "bb00000000000000000000b2|1|More|0", "bb00000000000000000000b1|2|Part A|0"],
                Read(connection, $"SELECT ProviderID, Position, Name, IsSpecial FROM Metadata_Ordering_Group {Where()} ORDER BY Position"));
            Assert.Equal(
                [$"bb00000000000000000000b0|0|{SpecialID}", $"bb00000000000000000000b1|0|{SecondEpisodeID}", $"bb00000000000000000000b1|1|{FirstEpisodeID}"],
                Read(connection, $"SELECT GroupID, Position, EpisodeID FROM Metadata_Ordering_Entry {Where()} ORDER BY GroupID, Position"));
            Assert.Equal(
                [$"{ShowID}|{FreshShowID}|0|0", $"{ShowID}|4242|1|1"],
                Read(connection, $"SELECT BaseID, SuggestedID, Kind, Ordering FROM Metadata_Suggestion {Where()} ORDER BY Ordering"));

            // The English text first, as the main title; the original title once.
            Assert.Equal(
                [
                    $"{Kind(MetadataEntityType.Series)}|{ShowID}|en|US|1|A Show|0", $"{Kind(MetadataEntityType.Series)}|{ShowID}|ja|JP|2|番組|1", $"{Kind(MetadataEntityType.Series)}|{ShowID}|fr|FR|2|Le Show|2",
                    $"{Kind(MetadataEntityType.Series)}|{FreshShowID}|en|US|1|Second|0", $"{Kind(MetadataEntityType.Series)}|{FreshShowID}|de|DE|2|Die Zweite|1",
                ],
                Read(connection, $"SELECT EntityType, EntityID, LanguageCode, CountryCode, TitleType, Value, Ordering FROM Metadata_Title {Where("EntitySource")} AND EntityType = {Kind(MetadataEntityType.Series)} " +
                    "ORDER BY EntityID, Ordering"));
            Assert.Equal(
                [$"{SecondEpisodeID}|Second Title|0", $"{SpecialID}|The Special|0"],
                Read(connection, $"SELECT EntityID, Value, Ordering FROM Metadata_Title {Where("EntitySource")} AND EntityType = {Kind(MetadataEntityType.Episode)} ORDER BY EntityID, Ordering"));
            Assert.Equal(
                [$"{MovieID}|en|1|A Movie|0", $"{MovieID}|ja|2|映画|1", $"{MemberMovieID}|en|1|Another Movie|0"],
                Read(connection, $"SELECT EntityID, LanguageCode, TitleType, Value, Ordering FROM Metadata_Title {Where("EntitySource")} AND EntityType = {Kind(MetadataEntityType.Movie)} ORDER BY EntityID, Ordering"));
            Assert.Equal(
                [$"{ShowID}|en|About it.|0", $"{ShowID}|de|Darüber.|1"],
                Read(connection, $"SELECT EntityID, LanguageCode, Value, Ordering FROM Metadata_Overview {Where("EntitySource")} AND EntityType = {Kind(MetadataEntityType.Series)} ORDER BY EntityID, Ordering"));

            // The episode link carries the episode's season and numbers.
            Assert.Equal([$"{SpecialsID}|0|1"],
                Read(connection, $"SELECT ProviderSeasonID, SeasonNumber, EpisodeNumber FROM CrossRef_AniDB_Metadata_Episode WHERE Source = {tmdb} AND AnidbAnimeID = 9870001"));

            // The images keep the order they were linked in, the pinned poster second.
            Assert.Equal(["p1.jpg|0", "p2.jpg|1"],
                Read(connection, "SELECT i.ResourceID, e.Ordering FROM ShokoImage_Entity e INNER JOIN ShokoImage i ON i.ID = e.ImageID " +
                    $"WHERE e.EntitySource = {tmdb} AND e.EntityID = '{ShowID}' ORDER BY e.Ordering"));
        }
        finally
        {
            Cleanup(connection, restored, fixture.Backend);
        }
    }

    #endregion
}
