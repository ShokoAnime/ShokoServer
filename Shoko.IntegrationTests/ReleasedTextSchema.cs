using System.Collections.Generic;
using System.Data;
using System.Linq;
using Shoko.Server.Databases;
using Shoko.TestData.Schema;
using Xunit;

namespace Shoko.IntegrationTests;

/// <summary>
/// Puts back the released tables and columns the text store and the shared link tables replaced,
/// as a database upgraded from a released version has them until the drop steps run, and drops
/// them again through those steps.
/// </summary>
internal static class ReleasedTextSchema
{
    #region Constants

    /// <summary>
    /// The revision of the first drop step, the same on every backend; the others follow it in
    /// the order of <see cref="Targets"/>.
    /// </summary>
    private const int FirstDropRevision = 133;

    /// <summary>
    /// What the drop steps remove, in the order they run: a table, or a column of a table.
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
    /// The backend's drop steps, one per target of <see cref="Targets"/>, in the same order.
    /// </summary>
    /// <param name="fixture">The started server.</param>
    /// <returns>One step per target.</returns>
    public static IReadOnlyList<DatabaseCommand> DropSteps(DatabaseMigrationFixture fixture)
        => SchemaSteps.Get(fixture, [.. Enumerable.Range(FirstDropRevision, Targets.Count)]);

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
    /// Runs the backend's own drop steps for whatever of <see cref="Targets"/> is there.
    /// </summary>
    /// <param name="fixture">The started server.</param>
    /// <param name="connection">An open connection to the migrated database.</param>
    public static void Drop(DatabaseMigrationFixture fixture, IDbConnection connection)
    {
        var steps = DropSteps(fixture);
        var schema = SchemaSnapshot.Read(connection, fixture.Backend);
        for (var index = 0; index < steps.Count; index++)
        {
            var (table, column) = Targets[index];
            if (!schema.Tables.TryGetValue(table, out var columns) || (column is not null && !columns.ContainsKey(column)))
                continue;

            var step = steps[index];
            if (step.Type is DatabaseCommandType.CodedCommand)
            {
                var result = step.UpdateCommand!(connection);
                Assert.True(result.Item1, result.Item2);
                continue;
            }

            Sql.Execute(connection, step.Command!);
        }
    }

    /// <summary>
    /// Names what of <see cref="Targets"/> the database has.
    /// </summary>
    /// <param name="connection">An open connection to the migrated database.</param>
    /// <param name="backend">The backend, as <see cref="DatabaseMigrationFixture.Backend"/> names it.</param>
    /// <returns>Each table, or table and column, that exists, as <c>Table</c> or <c>Table.Column</c>.</returns>
    public static IReadOnlyList<string> Present(IDbConnection connection, string backend)
    {
        var schema = SchemaSnapshot.Read(connection, backend);
        return Targets
            .Where(target => schema.Tables.TryGetValue(target.Table, out var columns) && (target.Column is null || columns.ContainsKey(target.Column)))
            .Select(target => target.Column is null ? target.Table : $"{target.Table}.{target.Column}")
            .ToList();
    }

    #endregion
}
