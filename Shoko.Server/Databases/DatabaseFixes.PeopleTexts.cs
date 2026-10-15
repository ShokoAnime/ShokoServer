using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region People Texts | Tables

    /// <summary>
    ///   The texts the people tables kept on their rows: the table, its ID
    ///   column, the kind of entry its rows are, the column holding the text
    ///   and the text table it moves to.
    /// </summary>
    private static readonly IReadOnlyList<(string Table, string IDColumn, MetadataEntityType EntityType, string Column, string TextTable)> _peopleTexts =
    [
        ("Metadata_Creator", "Metadata_CreatorID", MetadataEntityType.Creator, "Name", "Metadata_Title"),
        ("Metadata_Creator", "Metadata_CreatorID", MetadataEntityType.Creator, "Description", "Metadata_Overview"),
        ("Metadata_Character", "Metadata_CharacterID", MetadataEntityType.Character, "Name", "Metadata_Title"),
        ("Metadata_Character", "Metadata_CharacterID", MetadataEntityType.Character, "Description", "Metadata_Overview"),
    ];

    #endregion

    #region People Texts | Steps

    /// <summary>
    ///   Copies the names and descriptions of the stored creators and
    ///   characters from <c>Metadata_Creator</c> and <c>Metadata_Character</c>
    ///   into <c>Metadata_Title</c> and <c>Metadata_Overview</c>, under each
    ///   person's own ID and source, before the columns go.
    /// </summary>
    /// <remarks>
    ///   A name is copied as the main title in no known language and a
    ///   description as an English overview, as each was presented, ahead of
    ///   the person's other texts from its source. A blank one is left out. A
    ///   run again finds the text it stored and stores it once more in the
    ///   same place.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyPeopleTexts(object connection)
        => RunTextCopy(connection, "people", "Metadata_Title and Metadata_Overview", (transaction, report) =>
            _peopleTexts.Sum(texts => CopyPeopleTexts(transaction, texts, report)));

    #endregion

    #region People Texts | Helpers

    /// <summary>
    ///   Copies the texts one column of a people table kept, a page of people
    ///   at a time, each ahead of the person's other texts from its source.
    /// </summary>
    /// <remarks>
    ///   The texts before the first gap in a person's positions make room at
    ///   the front, so a run again, once it removed the text it stored, finds
    ///   the room free.
    /// </remarks>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="texts">The table, its ID column, the kind of entry, the column and the text table.</param>
    /// <param name="report">Called with each progress message.</param>
    /// <returns>How many texts it copied.</returns>
    private static int CopyPeopleTexts(
        DbTransaction transaction,
        (string Table, string IDColumn, MetadataEntityType EntityType, string Column, string TextTable) texts,
        Action<string> report
    )
    {
        var isTitle = texts.TextTable is "Metadata_Title";
        var textIDColumn = isTitle ? "Metadata_TitleID" : "Metadata_OverviewID";
        var (titleType, language, languageCode) = isTitle
            ? (TitleType.Main, TitleLanguage.Unknown, "unk")
            : (TitleType.None, TitleLanguage.English, "en");

        // What each person's own source stored, in order, with the value only where it could be the copied text.
        var copiedKind = isTitle ? $"TitleType = {(int)TitleType.Main} AND " : string.Empty;
        var stored = Read(
                transaction,
                $"SELECT EntitySource, EntityID, {textIDColumn}, Ordering, " +
                $"CASE WHEN {copiedKind}LanguageCode = '{languageCode}' AND CountryCode IS NULL THEN Value ELSE NULL END FROM {texts.TextTable} " +
                $"WHERE EntityType = {KindNumber(texts.EntityType)} AND Source = EntitySource AND ReferenceID IS NULL"
            )
            .GroupBy(row => (Source: AsInt(row[0]), ID: row[1] as string ?? string.Empty))
            .ToDictionary(group => group.Key, group => group.OrderBy(row => AsInt(row[3])).ThenBy(row => AsInt(row[2])).ToList());

        var copied = 0;
        var ids = ReadIDs(transaction, $"SELECT {texts.IDColumn} FROM {texts.Table} ORDER BY {texts.IDColumn}");
        var progress = new StartupProgress(report, $"Copying {texts.EntityType.Value} {(isTitle ? "names" : "descriptions")}", ids.Count);
        foreach (var page in ids.Chunk(TextEntriesPerPage))
        {
            var removed = new List<int>();
            var shifted = new List<int>();
            var inserted = new List<(int Source, CopiedText Text)>();
            var range = $"{texts.IDColumn} BETWEEN {page[0]} AND {page[^1]}";
            foreach (var row in Read(transaction, $"SELECT Source, ProviderID, {texts.Column} FROM {texts.Table} WHERE {range}"))
            {
                if (row[2] is not string value || string.IsNullOrWhiteSpace(value))
                    continue;

                var source = AsInt(row[0]);
                var entityID = row[1] as string ?? string.Empty;
                var own = stored.GetValueOrDefault((source, entityID)) ?? [];

                // A text this step stored before, which it stores again in the same place.
                if (own.FirstOrDefault(text => string.Equals(text[4] as string, value, StringComparison.Ordinal)) is { } earlier)
                {
                    removed.Add(AsInt(earlier[2]));
                    own = [.. own.Where(text => !ReferenceEquals(text, earlier))];
                }

                var gap = 0;
                while (gap < own.Count && AsInt(own[gap][3]) == gap)
                    gap++;
                shifted.AddRange(own.Take(gap).Select(text => AsInt(text[2])));
                inserted.Add((source, new(texts.EntityType, entityID, language, languageCode, null, value, 0, titleType)));
            }

            foreach (var chunk in removed.Chunk(TmdbIDsPerList))
                Execute(transaction, $"DELETE FROM {texts.TextTable} WHERE {textIDColumn} IN ({string.Join(", ", chunk)})");
            foreach (var chunk in shifted.Chunk(TmdbIDsPerList))
                Execute(transaction, $"UPDATE {texts.TextTable} SET Ordering = Ordering + 1 WHERE {textIDColumn} IN ({string.Join(", ", chunk)})");
            foreach (var bySource in inserted.GroupBy(text => text.Source))
            {
                var source = MetadataNumberRegistry.GetSource((byte)bySource.Key);
                InsertTexts(transaction, texts.TextTable, [.. bySource.Select(text => text.Text)], source, source);
            }

            copied += inserted.Count;
            progress.Advance(page.Length);
        }

        return copied;
    }

    #endregion
}
