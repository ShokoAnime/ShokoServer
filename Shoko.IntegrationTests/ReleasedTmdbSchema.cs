using System.Collections.Generic;
using System.Data;
using System.Linq;
using Shoko.TestData.Schema;

namespace Shoko.IntegrationTests;

/// <summary>
/// Puts back the TMDB tables the shared metadata tables replaced, with the columns the copy steps
/// and the text steps before them read, as a database upgraded from a released version has them
/// until the drop steps run.
/// </summary>
internal static class ReleasedTmdbSchema
{
    #region Constants

    /// <summary>
    /// The TMDB tables the copy steps read, in the order the drop steps remove them.
    /// </summary>
    public static readonly IReadOnlyList<string> Tables =
    [
        "TMDB_AlternateOrdering_Episode",
        "TMDB_AlternateOrdering_Season",
        "TMDB_AlternateOrdering",
        "TMDB_Collection_Movie",
        "TMDB_Collection",
        "TMDB_Company_Entity",
        "TMDB_Company",
        "TMDB_Episode_Cast",
        "TMDB_Episode_Crew",
        "TMDB_Movie_Cast",
        "TMDB_Movie_Crew",
        "TMDB_Show_Network",
        "TMDB_Network",
        "TMDB_Suggestion",
        "TMDB_Episode",
        "TMDB_Season",
        "TMDB_Movie",
        "TMDB_Show",
        "TMDB_Person",
    ];

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds back the TMDB tables that are missing.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    /// <returns>The tables it added.</returns>
    public static IReadOnlyList<string> Restore(IDbConnection connection, string backend)
    {
        var (id, number, text, date, real, flag) = backend switch
        {
            "SQLServer" => ("INT IDENTITY(1,1) NOT NULL PRIMARY KEY", "INT", "NVARCHAR(MAX)", "DATETIME2", "FLOAT", "BIT"),
            "MySQL" => ("INT NOT NULL AUTO_INCREMENT PRIMARY KEY", "INT", "TEXT", "DATETIME", "DOUBLE", "BIT"),
            _ => ("INTEGER PRIMARY KEY AUTOINCREMENT", "INTEGER", "TEXT", "DATETIME", "REAL", "INTEGER"),
        };
        var key = backend is "MySQL" ? "VARCHAR(64)" : text;
        var tables = new Dictionary<string, string>
        {
            ["TMDB_Show"] = $"TMDB_ShowID {id}, TmdbShowID {number} NOT NULL, EnglishTitle {text} NOT NULL, EnglishOverview {text} NOT NULL, OriginalTitle {text} NOT NULL, " +
                $"OriginalLanguageCode {text} NOT NULL, IsRestricted {flag} NOT NULL, Genres {text} NOT NULL, Keywords {text} NULL, ContentRatings {text} NOT NULL, " +
                $"ProductionCountries {text} NULL, EpisodeCount {number} NOT NULL, SeasonCount {number} NOT NULL, AlternateOrderingCount {number} NOT NULL, UserRating {real} NOT NULL, " +
                $"UserVotes {number} NOT NULL, FirstAiredAt {date} NULL, LastAiredAt {date} NULL, " +
                $"CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, TvdbShowID {number} NULL, PosterPath {text} NULL, BackdropPath {text} NULL, PreferredOrderingID {text} NULL, " +
                $"EnglishTitleListed {flag} NOT NULL DEFAULT 0, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Season"] = $"TMDB_SeasonID {id}, TmdbShowID {number} NOT NULL, TmdbSeasonID {number} NOT NULL, EnglishTitle {text} NOT NULL, EnglishOverview {text} NOT NULL, " +
                $"EpisodeCount {number} NOT NULL, SeasonNumber {number} NOT NULL, CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, PosterPath {text} NULL, " +
                $"EnglishTitleListed {flag} NOT NULL DEFAULT 0, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Episode"] = $"TMDB_EpisodeID {id}, TmdbShowID {number} NOT NULL, TmdbSeasonID {number} NOT NULL, TmdbEpisodeID {number} NOT NULL, EnglishTitle {text} NOT NULL, " +
                $"EnglishOverview {text} NOT NULL, SeasonNumber {number} NOT NULL, EpisodeNumber {number} NOT NULL, Runtime {number} NULL, UserRating {real} NOT NULL, " +
                $"UserVotes {number} NOT NULL, AiredAt {date} NULL, CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, TvdbEpisodeID {number} NULL, IsHidden {flag} NOT NULL, " +
                $"ThumbnailPath {text} NULL, EnglishTitleListed {flag} NOT NULL DEFAULT 0, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Movie"] = $"TMDB_MovieID {id}, TmdbMovieID {number} NOT NULL, TmdbCollectionID {number} NULL, EnglishTitle {text} NOT NULL, EnglishOverview {text} NOT NULL, " +
                $"OriginalTitle {text} NOT NULL, OriginalLanguageCode {text} NOT NULL, IsRestricted {flag} NOT NULL, IsVideo {flag} NOT NULL, Genres {text} NOT NULL, " +
                $"Keywords {text} NULL, ContentRatings {text} NOT NULL, ProductionCountries {text} NULL, Runtime {number} NULL, UserRating {real} NOT NULL, " +
                $"UserVotes {number} NOT NULL, ReleasedAt {date} NULL, CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, ImdbMovieID {text} NULL, PosterPath {text} NULL, " +
                $"BackdropPath {text} NULL, EnglishTitleListed {flag} NOT NULL DEFAULT 0, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Collection"] = $"TMDB_CollectionID {id}, TmdbCollectionID {number} NOT NULL, EnglishTitle {text} NOT NULL, EnglishOverview {text} NOT NULL, MovieCount {number} NOT NULL, " +
                $"CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, EnglishTitleListed {flag} NOT NULL DEFAULT 0, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Collection_Movie"] = $"TMDB_Collection_MovieID {id}, TmdbCollectionID {number} NOT NULL, TmdbMovieID {number} NOT NULL, Ordering {number} NOT NULL",
            ["TMDB_Person"] = $"TMDB_PersonID {id}, TmdbPersonID {number} NOT NULL, EnglishName {text} NOT NULL, EnglishBiography {text} NOT NULL, Gender {number} NOT NULL, " +
                $"IsRestricted {flag} NOT NULL, BirthDay {date} NULL, DeathDay {date} NULL, PlaceOfBirth {text} NULL, CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL, " +
                $"LastOrphanedAt {date} NULL, ImdbPersonID {text} NULL, EnglishOverviewListed {flag} NOT NULL DEFAULT 0",
            ["TMDB_Company"] = $"TMDB_CompanyID {id}, TmdbCompanyID {number} NOT NULL, Name {text} NOT NULL, CountryOfOrigin {text} NOT NULL",
            ["TMDB_Company_Entity"] = $"TMDB_Company_EntityID {id}, TmdbCompanyID {number} NOT NULL, TmdbEntityType {number} NOT NULL, TmdbEntityID {number} NOT NULL, Ordering {number} NOT NULL",
            ["TMDB_Network"] = $"TMDB_NetworkID {id}, TmdbNetworkID {number} NOT NULL, Name {text} NOT NULL, CountryOfOrigin {text} NOT NULL, LastOrphanedAt {date} NULL",
            ["TMDB_Show_Network"] = $"TMDB_Show_NetworkID {id}, TmdbShowID {number} NOT NULL, TmdbNetworkID {number} NOT NULL, Ordering {number} NOT NULL",
            ["TMDB_Episode_Cast"] = $"TMDB_Episode_CastID {id}, TmdbShowID {number} NOT NULL, TmdbSeasonID {number} NOT NULL, TmdbEpisodeID {number} NOT NULL, TmdbPersonID {number} NOT NULL, " +
                $"TmdbCreditID {text} NOT NULL, CharacterName {text} NOT NULL, IsGuestRole {flag} NOT NULL, Ordering {number} NOT NULL",
            ["TMDB_Episode_Crew"] = $"TMDB_Episode_CrewID {id}, TmdbShowID {number} NOT NULL, TmdbSeasonID {number} NOT NULL, TmdbEpisodeID {number} NOT NULL, TmdbPersonID {number} NOT NULL, " +
                $"TmdbCreditID {key} NOT NULL, Job {key} NOT NULL, Department {key} NOT NULL",
            ["TMDB_Movie_Cast"] = $"TMDB_Movie_CastID {id}, TmdbMovieID {number} NOT NULL, TmdbPersonID {number} NOT NULL, TmdbCreditID {text} NOT NULL, CharacterName {text} NOT NULL, " +
                $"Ordering {number} NOT NULL",
            ["TMDB_Movie_Crew"] = $"TMDB_Movie_CrewID {id}, TmdbMovieID {number} NOT NULL, TmdbPersonID {number} NOT NULL, TmdbCreditID {key} NOT NULL, Job {key} NOT NULL, " +
                $"Department {key} NOT NULL",
            ["TMDB_AlternateOrdering"] = $"TMDB_AlternateOrderingID {id}, TmdbShowID {number} NOT NULL, TmdbNetworkID {number} NULL, TmdbEpisodeGroupCollectionID {key} NOT NULL, " +
                $"EnglishTitle {text} NOT NULL, EnglishOverview {text} NOT NULL, EpisodeCount {number} NOT NULL, SeasonCount {number} NOT NULL, Type {number} NOT NULL, " +
                $"CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL",
            ["TMDB_AlternateOrdering_Season"] = $"TMDB_AlternateOrdering_SeasonID {id}, TmdbShowID {number} NOT NULL, TmdbEpisodeGroupCollectionID {key} NOT NULL, " +
                $"TmdbEpisodeGroupID {key} NOT NULL, EnglishTitle {text} NOT NULL, SeasonNumber {number} NOT NULL, EpisodeCount {number} NOT NULL, IsLocked {flag} NOT NULL, " +
                $"CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL",
            ["TMDB_AlternateOrdering_Episode"] = $"TMDB_AlternateOrdering_EpisodeID {id}, TmdbShowID {number} NOT NULL, TmdbEpisodeGroupCollectionID {key} NOT NULL, " +
                $"TmdbEpisodeGroupID {key} NOT NULL, TmdbEpisodeID {number} NOT NULL, SeasonNumber {number} NOT NULL, EpisodeNumber {number} NOT NULL, " +
                $"CreatedAt {date} NOT NULL, LastUpdatedAt {date} NOT NULL",
            ["TMDB_Suggestion"] = $"TMDB_SuggestionID {id}, TmdbEntityType {number} NOT NULL, TmdbEntityID {number} NOT NULL, SuggestedTmdbEntityID {number} NOT NULL, Kind {number} NOT NULL, " +
                $"Ordering {number} NOT NULL",
        };
        var schema = SchemaSnapshot.Read(connection, backend);
        var added = new List<string>();
        foreach (var table in Tables.Reverse().Where(table => !schema.Tables.ContainsKey(table)))
        {
            Sql.Execute(connection, $"CREATE TABLE {table} ({tables[table]})");
            added.Add(table);
        }

        return added;
    }

    /// <summary>
    /// Names the TMDB tables the database has.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    /// <returns>The tables that exist.</returns>
    public static IReadOnlyList<string> Present(IDbConnection connection, string backend)
    {
        var schema = SchemaSnapshot.Read(connection, backend);
        return [.. Tables.Where(schema.Tables.ContainsKey)];
    }

    /// <summary>
    /// Drops tables <see cref="Restore"/> added.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="tables">The tables to drop.</param>
    public static void Drop(IDbConnection connection, IEnumerable<string> tables)
    {
        foreach (var table in tables)
            Sql.Execute(connection, $"DROP TABLE {table}");
    }

    #endregion
}
