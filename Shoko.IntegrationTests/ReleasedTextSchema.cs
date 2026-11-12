using System.Collections.Generic;
using System.Data;
using Shoko.TestData.Schema;

namespace Shoko.IntegrationTests;

/// <summary>
/// Puts back the released tables and columns the text store and the shared link tables replaced,
/// as a database upgraded from a released version has them until the drop steps run, and drops
/// them again by plain SQL.
/// </summary>
internal static class ReleasedTextSchema
{
    #region Constants

    /// <summary>
    /// What the drop steps remove: a table, or a column of a table.
    /// </summary>
    public static readonly IReadOnlyList<(string Table, string? Column)> Targets =
    [
        ("AniDB_Anime_Title", null),
        ("AniDB_Episode_Title", null),
        ("TMDB_Title", null),
        ("TMDB_Overview", null),
        ("CrossRef_AniDB_TMDB_Show", null),
        ("CrossRef_AniDB_TMDB_Movie", null),
        ("CrossRef_AniDB_TMDB_Episode", null),
        ("AniDB_Anime", "AllTitles"),
        ("AniDB_Tag", "TagNameOverride"),
        ("TMDB_Person", "Aliases"),
        ("AnimeSeries", "SeriesNameOverride"),
        ("AnimeEpisode", "EpisodeNameOverride"),
        ("AnimeGroup", "GroupName"),
        ("AnimeGroup", "Description"),
        ("AnimeGroup", "IsManuallyNamed"),
        ("AnimeGroup", "OverrideDescription"),
    ];

    #endregion

    #region Public Methods

    /// <summary>
    /// Adds back what the drop steps remove and is missing, with the columns the copy steps and
    /// the tests read. Added columns take <c>null</c>, so the core's own writes need not fill them.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    public static void Restore(IDbConnection connection, string backend)
    {
        var (id, number, text, addColumn) = backend switch
        {
            "SQLServer" => ("INT IDENTITY(1,1) NOT NULL PRIMARY KEY", "INT", "NVARCHAR(MAX)", "ADD"),
            "MySQL" => ("INT NOT NULL AUTO_INCREMENT PRIMARY KEY", "INT", "TEXT", "ADD COLUMN"),
            _ => ("INTEGER PRIMARY KEY AUTOINCREMENT", "INTEGER", "TEXT", "ADD COLUMN"),
        };
        var tables = new Dictionary<string, string>
        {
            ["AniDB_Anime_Title"] = $"AniDB_Anime_TitleID {id}, AnimeID {number} NOT NULL, TitleType {text} NOT NULL, Language {text} NOT NULL, Title {text} NOT NULL",
            ["AniDB_Episode_Title"] = $"AniDB_Episode_TitleID {id}, AniDB_EpisodeID {number} NOT NULL, Language {text} NOT NULL, Title {text} NOT NULL",
            ["TMDB_Title"] = $"TMDB_TitleID {id}, ParentID {number} NOT NULL, ParentType {number} NOT NULL, LanguageCode {text} NOT NULL, CountryCode {text} NOT NULL, Value {text} NOT NULL",
            ["TMDB_Overview"] = $"TMDB_OverviewID {id}, ParentID {number} NOT NULL, ParentType {number} NOT NULL, LanguageCode {text} NOT NULL, CountryCode {text} NOT NULL, Value {text} NOT NULL",
            ["CrossRef_AniDB_TMDB_Show"] = $"CrossRef_AniDB_TMDB_ShowID {id}, AnidbAnimeID {number} NOT NULL, TmdbShowID {number} NOT NULL, MatchRating {number} NOT NULL",
            ["CrossRef_AniDB_TMDB_Movie"] = $"CrossRef_AniDB_TMDB_MovieID {id}, AnidbAnimeID {number} NOT NULL, AnidbEpisodeID {number} NOT NULL, TmdbMovieID {number} NOT NULL, MatchRating {number} NOT NULL",
            ["CrossRef_AniDB_TMDB_Episode"] = $"CrossRef_AniDB_TMDB_EpisodeID {id}, AnidbAnimeID {number} NOT NULL, AnidbEpisodeID {number} NOT NULL, TmdbShowID {number} NOT NULL, " +
                $"TmdbEpisodeID {number} NOT NULL, Ordering {number} NOT NULL, MatchRating {number} NOT NULL",
        };
        var schema = SchemaSnapshot.Read(connection, backend);
        foreach (var (table, column) in Targets)
        {
            if (column is null)
            {
                if (!schema.Tables.ContainsKey(table))
                    Sql.Execute(connection, $"CREATE TABLE {table} ({tables[table]})");
                continue;
            }

            // A column of a table that is gone itself is put back with its table, if at all.
            if (schema.Tables.TryGetValue(table, out var columns) && !columns.ContainsKey(column))
                Sql.Execute(connection, $"ALTER TABLE {table} {addColumn} {column} {(column is "IsManuallyNamed" or "OverrideDescription" ? number : text)} NULL");
        }
    }

    /// <summary>
    /// Drops whatever of <see cref="Targets"/> is there, by plain SQL.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    public static void Drop(IDbConnection connection, string backend)
    {
        var schema = SchemaSnapshot.Read(connection, backend);
        foreach (var (table, column) in Targets)
        {
            if (!schema.Tables.TryGetValue(table, out var columns))
                continue;

            if (column is null)
                Sql.Execute(connection, $"DROP TABLE {table}");
            else if (columns.ContainsKey(column))
                Sql.Execute(connection, $"ALTER TABLE {table} DROP COLUMN {column}");
        }
    }

    #endregion
}
