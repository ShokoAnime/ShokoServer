using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Generic Titles | Steps

    /// <summary>
    ///   Spells the language codes of AniDB's own titles in their canonical
    ///   casing, such as <c>en</c> for <c>EN</c> or <c>pt-BR</c> for
    ///   <c>pt-br</c>, which the copy from the old title tables kept as spelled.
    /// </summary>
    /// <remarks>
    ///   A code that is its language's code, ignoring case, takes the
    ///   language's spelling; any other is lower-cased, so <c>cz</c> or
    ///   <c>zh-yue</c> keep their spelling. Runs once per language and code.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> FixAnidbTitleLanguageCodeCasing(object connection)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not ConnectionState.Open)
                dbConnection.Open();

            // The servers compare text ignoring case, so a change of casing is
            // only seen through a binary comparison.
            Func<string, string> differs = dbConnection switch
            {
                SqliteConnection => code => $"LanguageCode <> '{code}'",
                MySqlConnection => code => $"CAST(LanguageCode AS BINARY) <> CAST('{code}' AS BINARY)",
                SqlConnection => code => $"LanguageCode COLLATE Latin1_General_BIN2 <> N'{code}'",
                _ => throw new NotSupportedException($"Unknown database connection {dbConnection.GetType().Name}."),
            };

            using var transaction = dbConnection.BeginTransaction();
            var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
            var anidbTitles = $"FROM Metadata_Title WHERE EntitySource = {anidb} AND Source = {anidb}";
            var pairs = Read(transaction, $"SELECT DISTINCT Language, LOWER(LanguageCode) {anidbTitles}")
                .Select(row => (Language: row[0] as string ?? string.Empty, Code: row[1] as string ?? string.Empty))
                .Distinct()
                .ToList();
            var changed = 0;
            foreach (var (language, code) in pairs)
            {
                var canonical = language.GetTitleLanguage().GetString();
                var target = string.Equals(code, canonical, StringComparison.OrdinalIgnoreCase) ? canonical : code;
                changed += ExecuteCount(
                    transaction,
                    $"UPDATE Metadata_Title SET LanguageCode = '{Escape(target)}' WHERE EntitySource = {anidb} AND Source = {anidb} " +
                    $"AND Language = '{Escape(language)}' AND LOWER(LanguageCode) = '{Escape(code)}' AND {differs(Escape(target))}"
                );
            }

            transaction.Commit();
            _logger.Info("Spelled the language codes of {Count} AniDB titles in their canonical casing.", changed);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    /// <summary>
    ///   Removes every generic episode and season title stored before, from
    ///   every source, AniDB's included, as the core synthesizes them instead:
    ///   such as <c>Episode 5</c> on episode 5, <c>Episode S1</c> on special 1,
    ///   <c>第5話</c> or <c>Season 2</c> on season 2. Every pick of them goes too.
    /// </summary>
    /// <remarks>
    ///   Uses the same check as the text store's guard,
    ///   <see cref="MetadataTextStore.IsGenericTitle"/>, with each entry's own
    ///   number, so a form with another number stays and running it again
    ///   removes nothing.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> RemoveGenericTitles(object connection)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not ConnectionState.Open)
                dbConnection.Open();

            using var transaction = dbConnection.BeginTransaction();
            var episode = KindNumber(MetadataEntityType.Episode);
            var season = KindNumber(MetadataEntityType.Season);
            var numbers = EntryNumbers(transaction);
            var generic = new HashSet<int>();
            using (var command = transaction.Connection!.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT Metadata_TitleID, EntitySource, EntityType, EntityID, Value FROM Metadata_Title " +
                    $"WHERE EntityType IN ({episode}, {season})";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    var kind = AsInt(reader.GetValue(2));
                    var entityType = kind == episode ? MetadataEntityType.Episode : MetadataEntityType.Season;
                    GenericEpisodeTitles.EntryNumber? number = numbers.TryGetValue((AsInt(reader.GetValue(1)), kind, reader.GetString(3)), out var found)
                        ? found
                        : null;
                    if (GenericEpisodeTitles.IsGenericFor(entityType, reader.IsDBNull(4) ? null : reader.GetString(4), number))
                        generic.Add(AsInt(reader.GetValue(0)));
                }
            }

            // A pick copies what it picks, so the picks of a removed title go
            // with it, and the picks of those.
            var picks = Read(transaction, "SELECT Metadata_TitleID, ReferenceID FROM Metadata_Title WHERE ReferenceID IS NOT NULL")
                .Select(row => (ID: AsInt(row[0]), ReferenceID: AsInt(row[1])))
                .ToList();
            var titles = generic.Count;
            int before;
            do
            {
                before = generic.Count;
                foreach (var (id, referenceID) in picks)
                    if (generic.Contains(referenceID))
                        generic.Add(id);
            } while (generic.Count > before);

            foreach (var chunk in generic.Order().Chunk(TmdbIDsPerList))
                Execute(transaction, $"DELETE FROM Metadata_Title WHERE Metadata_TitleID IN ({string.Join(", ", chunk.Select(Text))})");
            transaction.Commit();
            _logger.Info("Removed {Count} generic episode and season titles, and {Picks} picks of them, from Metadata_Title.", titles, generic.Count - titles);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    #endregion

    #region Generic Titles | Helpers

    /// <summary>
    ///   The number every stored season and episode's generic titles carry,
    ///   read as the core reads the entries back.
    /// </summary>
    /// <remarks>
    ///   AniDB's episodes and the Shoko episodes over them, the stored seasons
    ///   and episodes of every other source, and the groups of stored
    ///   orderings, numbered as <see cref="MetadataOrderingService.NumberGroups"/> does.
    /// </remarks>
    /// <param name="transaction">The open transaction.</param>
    /// <returns>Each entry's number, by its source's number, its kind's number and its ID.</returns>
    private static Dictionary<(int Source, int Kind, string ID), GenericEpisodeTitles.EntryNumber> EntryNumbers(DbTransaction transaction)
    {
        var episode = KindNumber(MetadataEntityType.Episode);
        var season = KindNumber(MetadataEntityType.Season);
        var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
        var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
        var numbers = new Dictionary<(int Source, int Kind, string ID), GenericEpisodeTitles.EntryNumber>();
        foreach (var row in Read(transaction, "SELECT EpisodeID, EpisodeType, EpisodeNumber FROM AniDB_Episode"))
            numbers[(anidb, episode, Text(AsInt(row[0])))] = new(AsInt(row[2]), (EpisodeType)AsInt(row[1]), AnidbForms: true);
        var shokoEpisodes = Read(
            transaction,
            "SELECT AnimeEpisode.AnimeEpisodeID, AniDB_Episode.EpisodeType, AniDB_Episode.EpisodeNumber FROM AnimeEpisode " +
            "INNER JOIN AniDB_Episode ON AniDB_Episode.EpisodeID = AnimeEpisode.AniDB_EpisodeID"
        );
        foreach (var row in shokoEpisodes)
            numbers[(shoko, episode, Text(AsInt(row[0])))] = new(AsInt(row[2]), (EpisodeType)AsInt(row[1]), AnidbForms: true);
        foreach (var row in Read(transaction, "SELECT Source, ProviderID, EpisodeNumber, Type FROM Metadata_Episode"))
            numbers[(AsInt(row[0]), episode, row[1] as string ?? string.Empty)] = new(AsInt(row[2]), (EpisodeType)AsInt(row[3]));
        foreach (var ((source, id), number) in OrderingGroupNumbers(transaction))
            numbers[(source, season, id)] = new(number);

        // A stored season wins over a group sharing its ID, which no source should give.
        foreach (var row in Read(transaction, "SELECT Source, ProviderID, SeasonNumber FROM Metadata_Season"))
            numbers[(AsInt(row[0]), season, row[1] as string ?? string.Empty)] = new(AsInt(row[2]));
        return numbers;
    }

    /// <summary>
    ///   The season number each group of a stored ordering is read back with:
    ///   <c>0</c> for the special group, else the number its source gave it,
    ///   else its place among the regular groups.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <returns>Each group's number, by its source's number and its ID.</returns>
    private static Dictionary<(int Source, string ID), int> OrderingGroupNumbers(DbTransaction transaction)
    {
        var rows = Read(
            transaction,
            "SELECT Source, ProviderID, OrderingID, IsSpecial, SeasonNumber FROM Metadata_Ordering_Group ORDER BY Position, Metadata_Ordering_GroupID"
        );
        var numbers = new Dictionary<(int Source, string ID), int>();
        foreach (var ordering in rows.GroupBy(row => (Source: AsInt(row[0]), OrderingID: row[2] as string ?? string.Empty)))
        {
            var groups = ordering.ToList();
            var seasonNumbers = MetadataOrderingService.NumberGroups([.. groups.Select(row => (AsBool(row[3]), row[4] is null ? (int?)null : AsInt(row[4])))]);
            for (var index = 0; index < groups.Count; index++)
                numbers[(ordering.Key.Source, groups[index][1] as string ?? string.Empty)] = seasonNumbers[index];
        }

        return numbers;
    }

    /// <summary>
    ///   Runs a statement in the transaction.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="sql">The statement.</param>
    /// <returns>How many rows it changed.</returns>
    private static int ExecuteCount(DbTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return command.ExecuteNonQuery();
    }

    /// <summary>
    ///   A value read from the database, ready to go between single quotes.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>The value with its quotes doubled.</returns>
    private static string Escape(string value)
        => value.Replace("'", "''", StringComparison.Ordinal);

    #endregion
}
