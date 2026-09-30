using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using NHibernate;
using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Metadata Sources | Old Spellings

    /// <summary>
    /// The old <c>DataSource</c> enum's member names, each mapped to the value
    /// that replaced it. Only the migrations read these spellings.
    /// </summary>
    private static readonly FrozenDictionary<string, string> _oldSourceSpellings = new Dictionary<string, string>
    {
        ["AniDB"] = "anidb",
        ["TMDB"] = "tmdb",
        ["TvDB"] = "tvdb",
        ["AniList"] = "anilist",
        ["Animeshon"] = "animeshon",
        ["Kitsu"] = "kitsu",
        ["MAL"] = "mal",
        ["FanartTV"] = "fanart-tv",
        ["IMDB"] = "imdb",
        ["OMDB"] = "omdb",
        ["TraktTv"] = "trakt",
        ["TPDB"] = "tpdb",
        ["MediUX"] = "mediux",
        ["SimKL"] = "simkl",
        ["Plugin"] = "plugin",
        ["LocallyGenerated"] = "generated",
        ["User"] = "user",
        ["Shoko"] = "shoko",
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> _oldSourceSpellingsByValue = _oldSourceSpellings
        .ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    /// <summary>
    /// Gets the old enum's spelling of a source value, which older versions
    /// hashed image IDs from and named image folders after.
    /// </summary>
    /// <param name="value">The source value.</param>
    /// <returns>The old spelling, or <c>null</c> if the enum had no such source.</returns>
    internal static string? GetOldSourceSpelling(string value)
        => _oldSourceSpellingsByValue.GetValueOrDefault(value);

    /// <summary>
    /// Turns text that the old enum or anything newer wrote into a source
    /// value. An old member name gives the value that replaced it, ignoring
    /// case, and other text is parsed.
    /// </summary>
    /// <param name="text">An old member name, or a value or alias.</param>
    /// <returns>
    /// The value, or <c>null</c> for blank text, the old <c>None</c> and text
    /// that is not a valid source.
    /// </returns>
    internal static string? ConvertOldSourceName(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Trim();
        if (string.Equals(text, "None", StringComparison.OrdinalIgnoreCase))
            return null;
        if (_oldSourceSpellings.TryGetValue(text, out var value))
            return value;

        return MetadataSource.TryParse(text, out var source) ? source.Value : null;
    }

    #endregion

    #region Metadata Numbers | Startup Check

    /// <summary>
    /// Every column that stores a source as its number, by table.
    /// </summary>
    internal static readonly IReadOnlyList<(string Table, string Column)> MetadataSourceColumns =
    [
        ("CrossRef_AniDB_Metadata_Series", "Source"),
        ("CrossRef_AniDB_Metadata_Movie", "Source"),
        ("CrossRef_AniDB_Metadata_Episode", "Source"),
        ("Metadata_Cast", "Source"),
        ("Metadata_Character", "Source"),
        ("Metadata_Collection", "Source"),
        ("Metadata_Collection_Member", "Source"),
        ("Metadata_ContentRating", "Source"),
        ("Metadata_Creator", "Source"),
        ("Metadata_Crew", "Source"),
        ("Metadata_Episode", "Source"),
        ("Metadata_Movie", "Source"),
        ("Metadata_Refresh", "Source"),
        ("Metadata_Ordering", "Source"),
        ("Metadata_Ordering", "SeriesSource"),
        ("Metadata_Ordering_Group", "Source"),
        ("Metadata_Ordering_Entry", "Source"),
        ("Metadata_Ordering_Entry", "EpisodeSource"),
        ("Metadata_Season", "Source"),
        ("Metadata_Series", "Source"),
        ("Metadata_Relation", "Source"),
        ("Metadata_Studio", "Source"),
        ("Metadata_Studio_Entry", "Source"),
        ("Metadata_Network", "Source"),
        ("Metadata_Network_Entry", "Source"),
        ("Metadata_Suggestion", "Source"),
        ("Metadata_Tag", "Source"),
        ("Metadata_Tag_Entry", "Source"),
        ("Metadata_Title", "Source"),
        ("Metadata_Title", "EntitySource"),
        ("Metadata_Overview", "Source"),
        ("Metadata_Overview", "EntitySource"),
        ("ShokoImage", "Source"),
        ("ShokoImage_Entity", "ImageSource"),
        ("ShokoImage_Entity", "EntitySource"),
        ("ShokoImage_Entity", "Source"),
        ("AiringSchedule", "SeriesSource"),
        ("EpisodeAiring", "EpisodeSource"),
    ];

    /// <summary>
    /// Every column that stores an entity type as its number, by table.
    /// </summary>
    internal static readonly IReadOnlyList<(string Table, string Column)> MetadataEntityTypeColumns =
    [
        ("CrossRef_AniDB_Metadata_Series", "ProviderType"),
        ("Metadata_Cast", "EntityType"),
        ("Metadata_Collection_Member", "MemberType"),
        ("Metadata_ContentRating", "EntityType"),
        ("Metadata_Crew", "EntityType"),
        ("Metadata_Network_Entry", "EntityType"),
        ("Metadata_Refresh", "EntityType"),
        ("Metadata_Relation", "BaseType"),
        ("Metadata_Relation", "RelatedType"),
        ("Metadata_Studio_Entry", "EntityType"),
        ("Metadata_Suggestion", "BaseType"),
        ("Metadata_Suggestion", "SuggestedType"),
        ("Metadata_Tag_Entry", "EntityType"),
        ("Metadata_Title", "EntityType"),
        ("Metadata_Overview", "EntityType"),
        ("ShokoImage_Entity", "EntityType"),
        ("TMDB_Suggestion", "TmdbEntityType"),
    ];

    /// <summary>
    /// Warns about stored source and entity type numbers that no longer name
    /// one, or whose value is now an alias of another. Runs on every start
    /// once the database is up, one <c>SELECT DISTINCT</c> per column, and
    /// never throws.
    /// </summary>
    /// <param name="databaseFactory">The database factory.</param>
    /// <param name="reportProgress">Called with each progress message, or <c>null</c>.</param>
    public static void CheckStoredMetadataNumbers(DatabaseFactory databaseFactory, Action<string>? reportProgress = null)
    {
        try
        {
            var progress = new StartupProgress(
                reportProgress ?? (_ => { }),
                "Checking stored metadata sources and entity types",
                MetadataSourceColumns.Count + MetadataEntityTypeColumns.Count
            );
            using var session = databaseFactory.SessionFactory.OpenStatelessSession();
            CheckStoredNumbers(session, MetadataSourceColumns, "source", MetadataNumberRegistry.FindProblem, progress);
            CheckStoredNumbers(session, MetadataEntityTypeColumns, "entity type", MetadataNumberRegistry.FindEntityTypeProblem, progress);
        }
        catch (Exception ex)
        {
            _logger.Warn(ex, "Unable to check the metadata sources and entity types stored in the database.");
        }
    }

    /// <summary>
    /// Warns about every distinct number in the given columns that the
    /// registry finds a problem with.
    /// </summary>
    /// <param name="session">The open session.</param>
    /// <param name="columns">The columns to read, by table.</param>
    /// <param name="kind">What the numbers stand for, for the log.</param>
    /// <param name="findProblem">Tells what is wrong with a number, or <c>null</c>.</param>
    /// <param name="progress">Advanced once per column read.</param>
    private static void CheckStoredNumbers(
        IStatelessSession session,
        IReadOnlyList<(string Table, string Column)> columns,
        string kind,
        Func<byte, string?> findProblem,
        StartupProgress progress
    )
    {
        foreach (var (table, column) in columns)
        {
            IList<object> numbers;
            try
            {
                numbers = session.CreateSQLQuery($"SELECT DISTINCT {column} FROM {table}").List<object>();
            }
            catch (Exception ex)
            {
                _logger.Warn(ex, "Unable to read the metadata {Kind} numbers stored in {Table}.{Column}.", kind, table, column);
                continue;
            }
            finally
            {
                progress.Advance();
            }

            foreach (var raw in numbers)
            {
                if (raw is null)
                    continue;

                var number = Convert.ToInt64(raw);
                var problem = number is < 0 or > 255
                    ? $"the number {number} is out of range for a metadata {kind}"
                    : findProblem((byte)number);
                if (problem is not null)
                    _logger.Warn("{Table}.{Column}: {Problem}.", table, column, problem);
            }
        }
    }

    #endregion
}
