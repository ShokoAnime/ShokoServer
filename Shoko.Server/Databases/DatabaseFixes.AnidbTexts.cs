using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Extensions;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Models.AniDB;
using Shoko.Server.Providers.AniDB;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region AniDB Texts | Tables

    /// <summary>
    ///   How many IDs of an old AniDB title table one page of a copy reads.
    /// </summary>
    private const int AnidbTitleRowsPerPage = 50_000;

    /// <summary>
    ///   An AniDB episode's type and number, which tell its generic title.
    /// </summary>
    /// <param name="Type">The episode's type.</param>
    /// <param name="Number">The episode's number.</param>
    private sealed record AnidbEpisodeNumber(EpisodeType Type, int Number);

    #endregion

    #region AniDB Texts | Steps

    /// <summary>
    ///   Copies the titles AniDB listed for its anime from
    ///   <c>AniDB_Anime_Title</c> into <c>Metadata_Title</c>, in AniDB's own
    ///   order, a page of anime at a time.
    /// </summary>
    /// <remarks>
    ///   AniDB's order is the one the anime's <c>AllTitles</c> column kept,
    ///   which each import rewrote in the order AniDB listed the titles; see
    ///   <see cref="OrderAnidbAnimeTitles"/>. The language codes are kept as
    ///   the rows spell them, upper-case ones included; backticks become
    ///   apostrophes, as the titles have always been read. Runs in one
    ///   transaction, and first removes what an earlier run of it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateAnidbAnimeTitles(object connection)
        => RunTextCopy(connection, "AniDB", "Metadata_Title", transaction =>
        {
            var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
            var series = MetadataNumberRegistry.GetNumber(MetadataEntityType.Series);
            Execute(transaction, $"DELETE FROM Metadata_Title WHERE EntitySource = {anidb} AND Source = {anidb} AND EntityType = {series}");

            var total = 0;
            var animeIDs = ReadIDs(transaction, "SELECT DISTINCT AnimeID FROM AniDB_Anime_Title ORDER BY AnimeID");
            foreach (var page in animeIDs.Chunk(TextEntriesPerPage))
            {
                var listings = new Dictionary<int, string?>();
                foreach (var row in Read(transaction, $"SELECT AnimeID, AllTitles FROM AniDB_Anime WHERE AnimeID BETWEEN {page[0]} AND {page[^1]}"))
                    listings.TryAdd(Convert.ToInt32(row[0], CultureInfo.InvariantCulture), row[1] as string);

                var byAnime = new Dictionary<int, List<object?[]>>();
                var rows = Read(transaction, $"SELECT AnimeID, Language, Title, TitleType FROM AniDB_Anime_Title WHERE AnimeID BETWEEN {page[0]} AND {page[^1]} ORDER BY AnimeID, AniDB_Anime_TitleID");
                foreach (var row in rows)
                {
                    if (row[2] is not string)
                        continue;

                    var animeID = Convert.ToInt32(row[0], CultureInfo.InvariantCulture);
                    if (!byAnime.TryGetValue(animeID, out var titles))
                        byAnime[animeID] = titles = [];
                    titles.Add(row);
                }

                var copied = new List<CopiedText>();
                foreach (var (animeID, titles) in byAnime)
                {
                    var orderings = OrderAnidbAnimeTitles([.. titles.Select(row => (string)row[2]!)], listings.GetValueOrDefault(animeID));
                    for (var index = 0; index < titles.Count; index++)
                    {
                        var row = titles[index];
                        var languageCode = row[1] as string ?? string.Empty;
                        copied.Add(new(
                            MetadataEntityType.Series,
                            animeID.ToString(CultureInfo.InvariantCulture),
                            languageCode.GetTitleLanguage(),
                            string.IsNullOrWhiteSpace(languageCode) ? "unk" : languageCode,
                            null,
                            ((string)row[2]!).Replace('`', '\''),
                            orderings[index],
                            TitleTypeOf(row[3])
                        ));
                    }
                }

                InsertTexts(transaction, "Metadata_Title", copied, MetadataSource.AniDB, MetadataSource.AniDB);
                total += copied.Count;
            }

            return total;
        });

    /// <summary>
    ///   Copies the titles AniDB listed for its episodes from
    ///   <c>AniDB_Episode_Title</c> into <c>Metadata_Title</c>, in the order
    ///   they were stored.
    /// </summary>
    /// <remarks>
    ///   The titles keep the order of their IDs until the anime is imported
    ///   again. The generic title (such as <c>Episode 5</c>) is not copied, as
    ///   it is made up when read. Language codes are kept as spelled, and
    ///   English titles are stored as the main ones. Runs in one transaction,
    ///   first removing what an earlier run wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateAnidbEpisodeTitles(object connection)
        => RunTextCopy(connection, "AniDB", "Metadata_Title", transaction =>
        {
            var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
            var episode = MetadataNumberRegistry.GetNumber(MetadataEntityType.Episode);
            Execute(transaction, $"DELETE FROM Metadata_Title WHERE EntitySource = {anidb} AND Source = {anidb} AND EntityType = {episode}");

            var numbers = new Dictionary<int, AnidbEpisodeNumber>();
            foreach (var row in Read(transaction, "SELECT EpisodeID, EpisodeType, EpisodeNumber FROM AniDB_Episode"))
                numbers[Convert.ToInt32(row[0], CultureInfo.InvariantCulture)] = new(
                    (EpisodeType)Convert.ToInt32(row[1], CultureInfo.InvariantCulture),
                    Convert.ToInt32(row[2], CultureInfo.InvariantCulture)
                );

            var positions = new Dictionary<int, int>();
            return CopyAnidbTitles(transaction, "AniDB_Episode_Title", "AniDB_Episode_TitleID", "AniDB_EpisodeID, Language, Title", row =>
            {
                if (row[2] is not string value)
                    return null;

                var episodeID = Convert.ToInt32(row[0], CultureInfo.InvariantCulture);
                if (numbers.TryGetValue(episodeID, out var number) && AnidbTextListing.IsGeneric(value, number.Type, number.Number))
                    return null;

                var languageCode = row[1] as string ?? string.Empty;
                var language = languageCode.GetTitleLanguage();
                positions[episodeID] = positions.GetValueOrDefault(episodeID) + 1;
                return new(
                    MetadataEntityType.Episode,
                    episodeID.ToString(CultureInfo.InvariantCulture),
                    language,
                    string.IsNullOrWhiteSpace(languageCode) ? "unk" : languageCode,
                    null,
                    value,
                    positions[episodeID] - 1,
                    AnidbText.EpisodeTitleType(language)
                );
            });
        });

    /// <summary>
    ///   Copies the names the core gave the AniDB tags it renames from
    ///   <c>AniDB_Tag.TagNameOverride</c> into <c>Metadata_Title</c>, as the
    ///   core's overall preferred English title of each tag.
    /// </summary>
    /// <remarks>
    ///   Backticks become apostrophes, as the names have always been read.
    ///   Runs in one transaction, and first removes what an earlier run of
    ///   it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateAnidbTagOverrides(object connection)
        => RunTextCopy(connection, "AniDB", "Metadata_Title", transaction =>
        {
            var anidb = MetadataNumberRegistry.GetNumber(MetadataSource.AniDB);
            var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
            var tag = MetadataNumberRegistry.GetNumber(MetadataEntityType.Tag);
            Execute(transaction, $"DELETE FROM Metadata_Title WHERE EntitySource = {anidb} AND Source = {shoko} AND EntityType = {tag}");

            var copied = new List<CopiedText>();
            foreach (var row in Read(transaction, "SELECT TagID, TagNameOverride FROM AniDB_Tag WHERE TagNameOverride IS NOT NULL ORDER BY TagID"))
            {
                if (row[1] is not string { Length: > 0 } value)
                    continue;

                copied.Add(new(
                    MetadataEntityType.Tag,
                    Convert.ToInt32(row[0], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                    TitleLanguage.English,
                    "en",
                    null,
                    value.Replace('`', '\''),
                    0,
                    TitleType.Main
                ));
            }

            InsertTexts(transaction, "Metadata_Title", copied, MetadataSource.AniDB, MetadataSource.Shoko, TextPreference.Overall);
            return copied.Count;
        });

    #endregion

    #region AniDB Texts | Helpers

    /// <summary>
    ///   Copies the rows of an old AniDB title table into
    ///   <c>Metadata_Title</c>, a page of IDs at a time and in ID order, so
    ///   each entry's titles keep the order they were stored in.
    /// </summary>
    /// <remarks>
    ///   Each page is read whole before it is written, as MySQL and SQL
    ///   Server cannot write on a connection with a reader open.
    /// </remarks>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The old title table.</param>
    /// <param name="idColumn">Its ID column, which gives the stored order.</param>
    /// <param name="columns">The columns to read, in the order <paramref name="plan"/> takes them.</param>
    /// <param name="plan">Works out the title to copy from a row, or <c>null</c> to leave it out; called in ID order.</param>
    /// <returns>How many titles it copied.</returns>
    private static int CopyAnidbTitles(DbTransaction transaction, string table, string idColumn, string columns, Func<object?[], CopiedText?> plan)
    {
        var bounds = Read(transaction, $"SELECT MIN({idColumn}), MAX({idColumn}) FROM {table}")[0];
        if (bounds[0] is null || bounds[1] is null)
            return 0;

        var first = Convert.ToInt64(bounds[0], CultureInfo.InvariantCulture);
        var last = Convert.ToInt64(bounds[1], CultureInfo.InvariantCulture);
        var copied = 0;
        for (var start = first; start <= last; start += AnidbTitleRowsPerPage)
        {
            var end = Math.Min(last, start + AnidbTitleRowsPerPage - 1);
            var texts = new List<CopiedText>();
            foreach (var row in Read(transaction, $"SELECT {columns} FROM {table} WHERE {idColumn} BETWEEN {start} AND {end} ORDER BY {idColumn}"))
                if (plan(row) is { } text)
                    texts.Add(text);

            InsertTexts(transaction, "Metadata_Title", texts, MetadataSource.AniDB, MetadataSource.AniDB);
            copied += texts.Count;
        }

        return copied;
    }

    /// <summary>
    ///   The positions of an anime's old titles in AniDB's own order: the
    ///   order of the anime's <c>AllTitles</c> column.
    /// </summary>
    /// <remarks>
    ///   Titles with the same text take that text's places in the column in
    ///   the order of their IDs. Titles the column does not have go after
    ///   the rest, in the order of their IDs. Backticks and apostrophes are
    ///   the same when compared.
    /// </remarks>
    /// <param name="titles">The anime's titles, in the order of their IDs.</param>
    /// <param name="allTitles">The anime's <c>AllTitles</c> column, which joins the titles with <c>|</c>, or <c>null</c>.</param>
    /// <returns>The position of each title, from 0 up, in the order the titles were given.</returns>
    internal static int[] OrderAnidbAnimeTitles(IReadOnlyList<string> titles, string? allTitles)
    {
        var places = new Dictionary<string, Queue<int>>(StringComparer.Ordinal);
        var listed = string.IsNullOrEmpty(allTitles) ? [] : allTitles.Split('|');
        for (var index = 0; index < listed.Length; index++)
        {
            var key = listed[index].Replace('`', '\'');
            if (!places.TryGetValue(key, out var queue))
                places[key] = queue = new();
            queue.Enqueue(index);
        }

        var keys = new long[titles.Count];
        for (var index = 0; index < titles.Count; index++)
            keys[index] = places.TryGetValue(titles[index].Replace('`', '\''), out var queue) && queue.Count is not 0
                ? queue.Dequeue()
                : (long)listed.Length + index;

        var sorted = Enumerable.Range(0, titles.Count).OrderBy(index => keys[index]).ToArray();
        var orderings = new int[titles.Count];
        for (var position = 0; position < sorted.Length; position++)
            orderings[sorted[position]] = position;

        return orderings;
    }

    /// <summary>
    ///   The type of an old AniDB anime title, stored as its name or number.
    /// </summary>
    /// <param name="value">The stored value.</param>
    /// <returns>The type.</returns>
    private static TitleType TitleTypeOf(object? value)
        => value switch
        {
            null => TitleType.None,
            string name => name.GetTitleType(),
            _ => (TitleType)Convert.ToInt32(value, CultureInfo.InvariantCulture),
        };

    #endregion
}
