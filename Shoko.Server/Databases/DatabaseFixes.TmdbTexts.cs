using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Data.Sqlite;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Server;
using Shoko.Server.Services;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region TMDB Texts | Tables

    /// <summary>
    ///   How many rows one insert writes. Each row takes four parameters, so
    ///   this stays under SQL Server's limit of 2,100 per statement.
    /// </summary>
    private const int TextRowsPerInsert = 400;

    /// <summary>
    ///   How many entries one page of a copy reads, plans and writes, so a
    ///   large library never holds all of its texts in memory at once.
    /// </summary>
    private const int TextEntriesPerPage = 1000;

    /// <summary>
    ///   The TMDB tables whose rows keep an English title and overview, with
    ///   the entity type their texts belong to and the kind of entry the old
    ///   text tables named them by.
    /// </summary>
    private static readonly IReadOnlyList<TmdbTextTable> _tmdbTextTables =
    [
        new("TMDB_Show", "TmdbShowID", "EnglishTitle", "EnglishOverview", MetadataEntityType.Series, ForeignEntityType.Show),
        new("TMDB_Season", "TmdbSeasonID", "EnglishTitle", "EnglishOverview", MetadataEntityType.Season, ForeignEntityType.Season),
        new("TMDB_Episode", "TmdbEpisodeID", "EnglishTitle", "EnglishOverview", MetadataEntityType.Episode, ForeignEntityType.Episode),
        new("TMDB_Movie", "TmdbMovieID", "EnglishTitle", "EnglishOverview", MetadataEntityType.Movie, ForeignEntityType.Movie),
        new("TMDB_Collection", "TmdbCollectionID", "EnglishTitle", "EnglishOverview", MetadataEntityType.Collection, ForeignEntityType.Collection),
        new("TMDB_Person", "TmdbPersonID", null, "EnglishBiography", MetadataEntityType.Creator, ForeignEntityType.Person),
    ];

    /// <summary>
    ///   A TMDB table whose rows keep an English title or overview.
    /// </summary>
    /// <param name="Table">The table.</param>
    /// <param name="IDColumn">The column holding the TMDB ID.</param>
    /// <param name="TitleColumn">The column holding the English title, or <c>null</c> when the rows have none.</param>
    /// <param name="OverviewColumn">The column holding the English overview.</param>
    /// <param name="EntityType">The kind of entry the rows are.</param>
    /// <param name="ForeignType">The kind of entry the old text tables named them by.</param>
    private sealed record TmdbTextTable(string Table, string IDColumn, string? TitleColumn, string OverviewColumn, MetadataEntityType EntityType, ForeignEntityType ForeignType);

    /// <summary>
    ///   One row of the old TMDB title or overview table.
    /// </summary>
    /// <param name="ParentID">The entry's TMDB ID.</param>
    /// <param name="LanguageCode">The language code.</param>
    /// <param name="CountryCode">The country code, which may be empty.</param>
    /// <param name="Value">The text.</param>
    private sealed record OldTmdbText(int ParentID, string LanguageCode, string CountryCode, string Value);

    /// <summary>
    ///   A text to copy into the new title or overview table.
    /// </summary>
    /// <param name="EntityType">The kind of entry it belongs to.</param>
    /// <param name="EntityID">The entry's ID.</param>
    /// <param name="Language">The language.</param>
    /// <param name="LanguageCode">The language code.</param>
    /// <param name="CountryCode">The country code, or <c>null</c>.</param>
    /// <param name="Value">The text.</param>
    /// <param name="Ordering">Its position among the entry's texts from TMDB.</param>
    /// <param name="TitleType">The kind of title, or <see cref="TitleType.None"/> for an overview.</param>
    private sealed record CopiedText(MetadataEntityType EntityType, string EntityID, TitleLanguage Language, string LanguageCode, string? CountryCode, string Value, int Ordering, TitleType TitleType);

    #endregion

    #region TMDB Texts | Steps

    /// <summary>
    ///   Copies the titles TMDB listed for its shows, seasons, episodes,
    ///   movies and collections from <c>TMDB_Title</c> into
    ///   <c>Metadata_Title</c>, in the order they were listed.
    /// </summary>
    /// <remarks>
    ///   The American English title equal to the English title on the entry's
    ///   row is not copied; the row's flag says it was listed, and a gap in
    ///   the positions says where. An episode's generic title with its own
    ///   number, such as <c>Episode 5</c>, is not copied either, nor is a
    ///   season's English generic name, such as <c>Season 2</c>. Runs in one
    ///   transaction, and first removes what an earlier run of it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateTmdbTitles(object connection)
        => RunTextCopy(connection, "TMDB", "Metadata_Title", (transaction, report) =>
        {
            var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
            var creator = MetadataNumberRegistry.GetNumber(MetadataEntityType.Creator);
            Execute(transaction, $"DELETE FROM Metadata_Title WHERE EntitySource = {tmdb} AND Source = {tmdb} AND EntityType <> {creator}");
            var copied = 0;
            foreach (var table in _tmdbTextTables.Where(table => table.TitleColumn is not null))
            {
                Execute(transaction, $"UPDATE {table.Table} SET EnglishTitleListed = 0 WHERE EnglishTitleListed <> 0");
                copied += CopyTmdbTexts(
                    transaction, table, "TMDB_Title", "TMDB_TitleID", table.TitleColumn!, "Metadata_Title", "EnglishTitleListed", TitleType.Official, report
                );
            }

            return copied;
        });

    /// <summary>
    ///   Copies the overviews TMDB listed for its shows, seasons, episodes,
    ///   movies, collections and people from <c>TMDB_Overview</c> into
    ///   <c>Metadata_Overview</c>, in the order they were listed.
    /// </summary>
    /// <remarks>
    ///   The American English overview equal to the English overview on the
    ///   entry's row is not copied; the row's flag says it was listed, and a
    ///   gap in the positions says where. Runs in one transaction, and first
    ///   removes what an earlier run of it wrote. It is the largest copy, so
    ///   the memory it used is handed back once it is done.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateTmdbOverviews(object connection)
    {
        var result = RunTextCopy(connection, "TMDB", "Metadata_Overview", (transaction, report) =>
        {
            var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
            Execute(transaction, $"DELETE FROM Metadata_Overview WHERE EntitySource = {tmdb} AND Source = {tmdb}");
            var copied = 0;
            foreach (var table in _tmdbTextTables)
            {
                Execute(transaction, $"UPDATE {table.Table} SET EnglishOverviewListed = 0 WHERE EnglishOverviewListed <> 0");
                copied += CopyTmdbTexts(
                    transaction, table, "TMDB_Overview", "TMDB_OverviewID", table.OverviewColumn,
                    "Metadata_Overview", "EnglishOverviewListed", TitleType.None, report
                );
            }

            return copied;
        });
        ReleaseCopyMemory(connection);
        return result;
    }

    /// <summary>
    ///   Copies the other names TMDB gave its people from
    ///   <c>TMDB_Person.Aliases</c> into <c>Metadata_Title</c>, as synonyms in
    ///   an unknown language, in the order they were given.
    /// </summary>
    /// <remarks>
    ///   The column joins the names with <c>|||</c>; empty names are left
    ///   out. Runs in one transaction, and first removes what an earlier run
    ///   of it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateTmdbPersonAliases(object connection)
        => RunTextCopy(connection, "TMDB", "Metadata_Title", (transaction, report) =>
        {
            var tmdb = MetadataNumberRegistry.GetNumber(MetadataSource.TMDB);
            var creator = MetadataNumberRegistry.GetNumber(MetadataEntityType.Creator);
            Execute(transaction, $"DELETE FROM Metadata_Title WHERE EntitySource = {tmdb} AND Source = {tmdb} AND EntityType = {creator}");

            var total = 0;
            var personIDs = ReadIDs(transaction, "SELECT TmdbPersonID FROM TMDB_Person ORDER BY TmdbPersonID");
            var progress = new StartupProgress(report, "Copying TMDB person aliases", personIDs.Count);
            foreach (var page in personIDs.Chunk(TextEntriesPerPage))
            {
                var copied = new List<CopiedText>();
                var rows = Read(transaction, $"SELECT TmdbPersonID, Aliases FROM TMDB_Person WHERE TmdbPersonID BETWEEN {page[0]} AND {page[^1]} ORDER BY TmdbPersonID");
                foreach (var row in rows)
                {
                    var personID = Convert.ToInt32(row[0], CultureInfo.InvariantCulture);
                    var aliases = row[1] as string ?? string.Empty;
                    var ordering = 0;
                    foreach (var alias in aliases.Split("|||"))
                        if (!string.IsNullOrWhiteSpace(alias))
                            copied.Add(new(MetadataEntityType.Creator, personID.ToString(CultureInfo.InvariantCulture), TitleLanguage.Unknown, "unk", null, alias, ordering++, TitleType.Synonym));
                }

                InsertTexts(transaction, "Metadata_Title", copied, MetadataSource.TMDB, MetadataSource.TMDB);
                total += copied.Count;
                progress.Advance(page.Length);
            }

            return total;
        });

    #endregion

    #region TMDB Texts | Helpers

    /// <summary>
    ///   Runs one copy step in a transaction of its own, logging how many
    ///   texts it copied.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    /// <param name="source">The source whose texts are copied, for the log.</param>
    /// <param name="target">The table copied into, for the log.</param>
    /// <param name="copy">Copies the texts, reporting its progress to the action it is given, and returns how many.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    private static Tuple<bool, string?> RunTextCopy(object connection, string source, string target, Func<DbTransaction, Action<string>, int> copy)
    {
        try
        {
            var dbConnection = (DbConnection)connection;
            if (dbConnection.State is not ConnectionState.Open)
                dbConnection.Open();

            using var transaction = dbConnection.BeginTransaction();
            var count = copy(transaction, ReportToStartup());
            transaction.Commit();
            _logger.Info("Copied {Count} {Source} texts into {Table}.", count, source, target);
            return new(true, null);
        }
        catch (Exception ex)
        {
            return new(false, ex.ToString());
        }
    }

    /// <summary>
    ///   Hands back the memory a large copy used: the SQLite page cache it
    ///   filled and the garbage it left.
    /// </summary>
    /// <param name="connection">The open connection to the database.</param>
    private static void ReleaseCopyMemory(object connection)
    {
        if (connection is SqliteConnection { State: ConnectionState.Open } sqlite)
        {
            using var command = sqlite.CreateCommand();
            command.CommandText = "PRAGMA shrink_memory";
            command.ExecuteNonQuery();
        }

        GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
    }

    /// <summary>
    ///   Copies the old TMDB texts of one kind of entry into the new title or
    ///   overview table, a page of entries at a time, and flags the entries
    ///   that listed their English text.
    /// </summary>
    /// <remarks>
    ///   Each page is read whole before it is written, as MySQL and SQL
    ///   Server cannot write on a connection with a reader open.
    /// </remarks>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The table of the entries the texts belong to.</param>
    /// <param name="oldTable">The old text table.</param>
    /// <param name="oldIDColumn">The old table's ID column, which gives the listing order.</param>
    /// <param name="englishColumn">The column holding the entries' English text.</param>
    /// <param name="newTable">The new title or overview table.</param>
    /// <param name="listedColumn">The flag saying an entry listed its English text.</param>
    /// <param name="titleType">The kind of title to copy the texts as, or <see cref="TitleType.None"/> for overviews.</param>
    /// <param name="report">Called with each progress message.</param>
    /// <returns>How many texts it copied.</returns>
    private static int CopyTmdbTexts(
        DbTransaction transaction,
        TmdbTextTable table,
        string oldTable,
        string oldIDColumn,
        string englishColumn,
        string newTable,
        string listedColumn,
        TitleType titleType,
        Action<string> report
    )
    {
        var parentType = (int)table.ForeignType;
        var numberColumn = titleType is TitleType.None ? string.Empty : table.ForeignType switch
        {
            ForeignEntityType.Episode => ", EpisodeNumber",
            ForeignEntityType.Season => ", SeasonNumber",
            _ => string.Empty,
        };
        var withNumbers = numberColumn.Length > 0;
        var copied = 0;
        var parentIDs = ReadIDs(transaction, $"SELECT DISTINCT ParentID FROM {oldTable} WHERE ParentType = {parentType} ORDER BY ParentID");
        var label = $"Copying TMDB {table.ForeignType.ToString().ToLowerInvariant()} {(titleType is TitleType.None ? "overviews" : "titles")}";
        var progress = new StartupProgress(report, label, parentIDs.Count);
        report($"{label}...");
        foreach (var page in parentIDs.Chunk(TextEntriesPerPage))
        {
            var range = $"BETWEEN {page[0]} AND {page[^1]}";
            var english = new Dictionary<int, (string Text, int? Number)>();
            foreach (var row in Read(transaction, $"SELECT {table.IDColumn}, {englishColumn}{numberColumn} FROM {table.Table} WHERE {table.IDColumn} {range}"))
                english[Convert.ToInt32(row[0], CultureInfo.InvariantCulture)] = (
                    row[1] as string ?? string.Empty,
                    withNumbers ? Convert.ToInt32(row[2], CultureInfo.InvariantCulture) : null
                );

            var old = Read(transaction, $"SELECT ParentID, LanguageCode, CountryCode, Value FROM {oldTable} WHERE ParentType = {parentType} AND ParentID {range} ORDER BY ParentID, {oldIDColumn}")
                .Select(row => new OldTmdbText(
                    Convert.ToInt32(row[0], CultureInfo.InvariantCulture),
                    row[1] as string ?? string.Empty,
                    row[2] as string ?? string.Empty,
                    row[3] as string ?? string.Empty
                ));

            var (texts, listed) = PlanTmdbTexts(table, english, old, titleType);
            InsertTexts(transaction, newTable, texts, MetadataSource.TMDB, MetadataSource.TMDB);
            SetListed(transaction, table, listedColumn, listed);
            copied += texts.Count;
            progress.Advance(page.Length);
        }

        return copied;
    }

    /// <summary>
    ///   Works out which old TMDB texts of a page of entries to copy, and at
    ///   what positions, and which entries listed their English text.
    /// </summary>
    /// <param name="table">The table of the entries the texts belong to.</param>
    /// <param name="english">The entries' English text and, for episodes' and seasons' titles, their number, by TMDB ID.</param>
    /// <param name="old">The old texts, by entry and in the order they were listed.</param>
    /// <param name="titleType">The kind of title to copy the texts as, or <see cref="TitleType.None"/> for overviews.</param>
    /// <returns>The texts to copy, and the TMDB IDs of the entries that listed their English text.</returns>
    private static (List<CopiedText> Copied, List<int> Listed) PlanTmdbTexts(
        TmdbTextTable table,
        IReadOnlyDictionary<int, (string Text, int? Number)> english,
        IEnumerable<OldTmdbText> old,
        TitleType titleType
    )
    {
        var copied = new List<CopiedText>();
        var listed = new List<int>();
        foreach (var entry in old.GroupBy(text => text.ParentID))
        {
            var entityID = entry.Key.ToString(CultureInfo.InvariantCulture);
            var hasRow = english.TryGetValue(entry.Key, out var row);
            var episodeNumber = hasRow && table.EntityType == MetadataEntityType.Episode ? row.Number : null;
            var seasonNumber = hasRow && table.EntityType == MetadataEntityType.Season ? row.Number : null;
            int? gap = null;
            var position = 0;
            foreach (var text in entry)
            {
                // A season's English generic name is synthesized, not stored, so it leaves no gap.
                var genericSeason = seasonNumber is { } season && text.LanguageCode.Trim().Equals("en", StringComparison.OrdinalIgnoreCase) &&
                    GenericEpisodeTitles.IsGenericSeasonName(text.Value, season);
                if (gap is null && hasRow && IsTmdbEnglishDefault(text.LanguageCode, text.CountryCode, text.Value, row.Text))
                {
                    gap = genericSeason ? position : position++;
                    continue;
                }

                if (genericSeason || (episodeNumber is { } episode && GenericEpisodeTitles.IsGeneric(text.Value, EpisodeType.Episode, episode)))
                    continue;

                copied.Add(new(
                    table.EntityType,
                    entityID,
                    TmdbTextLanguage(text.LanguageCode, text.CountryCode),
                    string.IsNullOrWhiteSpace(text.LanguageCode) ? "unk" : text.LanguageCode,
                    string.IsNullOrWhiteSpace(text.CountryCode) ? null : text.CountryCode,
                    text.Value,
                    position++,
                    titleType
                ));
            }

            if (gap is not null)
                listed.Add(entry.Key);
        }

        return (copied, listed);
    }

    /// <summary>
    ///   Inserts the copied texts, many rows per statement.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The new title or overview table.</param>
    /// <param name="texts">The texts.</param>
    /// <param name="entitySource">The source of the entries the texts belong to.</param>
    /// <param name="source">The source that wrote the texts.</param>
    /// <param name="preference">How strongly the texts are preferred.</param>
    private static void InsertTexts(
        DbTransaction transaction,
        string table,
        IReadOnlyList<CopiedText> texts,
        MetadataSource entitySource,
        MetadataSource source,
        TextPreference preference = TextPreference.None
    )
    {
        var entities = MetadataNumberRegistry.GetNumber(entitySource);
        var writer = MetadataNumberRegistry.GetNumber(source);
        var preferred = (int)preference;
        var isTitle = table is "Metadata_Title";
        var columns = isTitle
            ? "EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, TitleType, Value, IsEnabled, Preference, Ordering, ReferenceID"
            : "EntitySource, EntityType, EntityID, Source, Language, LanguageCode, CountryCode, ScriptCode, Value, IsEnabled, Preference, Ordering, ReferenceID";
        // SQLite runs one prepared statement per row fastest; the servers
        // take many rows per statement.
        var rowsPerInsert = transaction.Connection is SqliteConnection ? 1 : TextRowsPerInsert;
        using var single = rowsPerInsert is 1 ? transaction.Connection!.CreateCommand() : null;
        foreach (var chunk in texts.Chunk(rowsPerInsert))
        {
            if (single is not null)
            {
                var text = chunk[0];
                if (single.Parameters.Count is 0)
                {
                    single.Transaction = transaction;
                    single.CommandText = isTitle
                        ? $"INSERT INTO {table} ({columns}) VALUES ({entities}, @t, @e, {writer}, @l, @c, @k, NULL, @y, @v, 1, {preferred}, @o, NULL)"
                        : $"INSERT INTO {table} ({columns}) VALUES ({entities}, @t, @e, {writer}, @l, @c, @k, NULL, @v, 1, {preferred}, @o, NULL)";
                    foreach (var name in (string[])["@t", "@e", "@l", "@c", "@k", "@y", "@v", "@o"])
                        if (isTitle || name is not "@y")
                            AddParameter(single, name, null);
                    single.Prepare();
                }

                single.Parameters["@t"].Value = (int)MetadataNumberRegistry.GetNumber(text.EntityType);
                single.Parameters["@e"].Value = text.EntityID;
                single.Parameters["@l"].Value = text.Language.GetString();
                single.Parameters["@c"].Value = text.LanguageCode;
                single.Parameters["@k"].Value = (object?)text.CountryCode ?? DBNull.Value;
                if (isTitle)
                    single.Parameters["@y"].Value = (int)text.TitleType;
                single.Parameters["@v"].Value = text.Value;
                single.Parameters["@o"].Value = text.Ordering;
                single.ExecuteNonQuery();
                continue;
            }

            using var command = transaction.Connection!.CreateCommand();
            command.Transaction = transaction;
            var sql = new StringBuilder($"INSERT INTO {table} ({columns}) VALUES ");
            for (var index = 0; index < chunk.Length; index++)
            {
                var text = chunk[index];
                if (index > 0)
                    sql.Append(", ");
                var titleType = isTitle ? $"{(int)text.TitleType}, " : string.Empty;
                sql.Append(CultureInfo.InvariantCulture, $"({entities}, {MetadataNumberRegistry.GetNumber(text.EntityType)}, @e{index}, {writer}, @l{index}, @c{index}, @k{index}, NULL, {titleType}@v{index}, 1, {preferred}, {text.Ordering}, NULL)");
                AddParameter(command, $"@e{index}", text.EntityID);
                AddParameter(command, $"@l{index}", text.Language.GetString());
                AddParameter(command, $"@c{index}", text.LanguageCode);
                AddParameter(command, $"@k{index}", text.CountryCode);
                AddParameter(command, $"@v{index}", text.Value);
            }

            command.CommandText = sql.ToString();
            command.ExecuteNonQuery();
        }
    }

    /// <summary>
    ///   Sets the flag saying an entry listed its English text, for the
    ///   entries that did.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The entries' table.</param>
    /// <param name="column">The flag's column.</param>
    /// <param name="ids">The TMDB IDs of the entries that listed their English text.</param>
    private static void SetListed(DbTransaction transaction, TmdbTextTable table, string column, IReadOnlyList<int> ids)
    {
        foreach (var chunk in ids.Order().Chunk(1000))
            Execute(transaction, $"UPDATE {table.Table} SET {column} = 1 WHERE {table.IDColumn} IN ({string.Join(", ", chunk.Select(id => id.ToString(CultureInfo.InvariantCulture)))})");
    }

    /// <summary>
    ///   Runs a statement in the transaction.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="sql">The statement.</param>
    private static void Execute(DbTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    /// <summary>
    ///   Reads every row a query returns, in the transaction.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="sql">The query.</param>
    /// <returns>The rows, each as its values, with <c>null</c> for a database null.</returns>
    private static List<object?[]> Read(DbTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
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

    /// <summary>
    ///   Reads the IDs a query returns, in the transaction.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="sql">The query, which selects one integer column.</param>
    /// <returns>The IDs, in the order the query gives them.</returns>
    private static List<int> ReadIDs(DbTransaction transaction, string sql)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
            ids.Add(Convert.ToInt32(reader.GetValue(0), CultureInfo.InvariantCulture));

        return ids;
    }

    /// <summary>
    ///   Counts the rows of a table, in the transaction.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The table.</param>
    /// <returns>The number of rows.</returns>
    private static int CountRows(DbTransaction transaction, string table)
    {
        using var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"SELECT COUNT(*) FROM {table}";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    ///   Whether a listed text is the American English one equal to the
    ///   English default on the entity's row.
    /// </summary>
    /// <param name="languageCode">The text's language code.</param>
    /// <param name="countryCode">The text's country code.</param>
    /// <param name="value">The text.</param>
    /// <param name="english">The English default on the entity's row.</param>
    /// <returns><c>true</c> when it is.</returns>
    private static bool IsTmdbEnglishDefault(string languageCode, string? countryCode, string value, string? english)
        => languageCode == "en" && countryCode == "US" && !string.IsNullOrEmpty(english) && string.Equals(value, english, StringComparison.Ordinal);

    /// <summary>
    ///   The language of a TMDB text, from its codes. TMDB's own <c>xx</c>
    ///   means no language and <c>cn</c> Cantonese; a code no language
    ///   matches is unknown, without reporting it.
    /// </summary>
    /// <param name="languageCode">The language code.</param>
    /// <param name="countryCode">The country code, which may be empty.</param>
    /// <returns>The language.</returns>
    internal static TitleLanguage TmdbTextLanguage(string? languageCode, string? countryCode)
    {
        var code = languageCode?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(code) || code is "xx")
            return TitleLanguage.None;
        if (code is "cn")
            return TitleLanguage.Chinese;
        if (!string.IsNullOrWhiteSpace(countryCode) && $"{code}-{countryCode.Trim()}".TryGetTitleLanguage(out var regional))
            return regional;
        return code.TryGetTitleLanguage(out var language) ? language : TitleLanguage.Unknown;
    }

    /// <summary>
    ///   Adds a parameter to a command.
    /// </summary>
    /// <param name="command">The command.</param>
    /// <param name="name">The parameter's name.</param>
    /// <param name="value">Its value, or <c>null</c> for a database null.</param>
    private static void AddParameter(DbCommand command, string name, string? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = (object?)value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    #endregion
}
