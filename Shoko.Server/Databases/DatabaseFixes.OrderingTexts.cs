using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Server.Services;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Ordering Texts | Steps

    /// <summary>
    ///   Copies the names and descriptions of the stored orderings and their
    ///   groups from <c>Metadata_Ordering</c> and <c>Metadata_Ordering_Group</c>
    ///   into <c>Metadata_Title</c> and <c>Metadata_Overview</c>, under each
    ///   ordering's and group's own ID and source, before the columns go.
    /// </summary>
    /// <remarks>
    ///   The texts are copied as they were kept, in no known language, the
    ///   names as main titles. A blank one is left out, and so is a group's
    ///   generic name for its own season number, which the core synthesizes.
    ///   A text already stored is not copied again.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> CopyOrderingTexts(object connection)
        => RunTextCopy(connection, "ordering", "Metadata_Title and Metadata_Overview", (transaction, _) =>
        {
            var groupNumbers = OrderingGroupNumbers(transaction);
            var titles = new List<(int Source, CopiedText Text)>();
            var overviews = new List<(int Source, CopiedText Text)>();
            ReadOrderingTexts(transaction, "Metadata_Ordering", MetadataEntityType.Ordering, titles, overviews, (_, _) => false);
            ReadOrderingTexts(
                transaction,
                "Metadata_Ordering_Group",
                MetadataEntityType.Season,
                titles,
                overviews,
                (source, text) => GenericEpisodeTitles.IsGenericForSeason(text.Value, groupNumbers.GetValueOrDefault((source, text.EntityID), -1))
            );

            return InsertNewOrderingTexts(transaction, "Metadata_Title", titles) + InsertNewOrderingTexts(transaction, "Metadata_Overview", overviews);
        });

    #endregion

    #region Ordering Texts | Helpers

    /// <summary>
    ///   Reads the names and descriptions kept on the rows of one table of
    ///   orderings or groups.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The table.</param>
    /// <param name="entityType">The kind of entry its rows are.</param>
    /// <param name="titles">Gets the names, with their source's number.</param>
    /// <param name="overviews">Gets the descriptions, with their source's number.</param>
    /// <param name="isGeneric">Whether a name is a generic one the core synthesizes, given its source's number.</param>
    private static void ReadOrderingTexts(
        DbTransaction transaction,
        string table,
        MetadataEntityType entityType,
        List<(int Source, CopiedText Text)> titles,
        List<(int Source, CopiedText Text)> overviews,
        Func<int, CopiedText, bool> isGeneric
    )
    {
        foreach (var row in Read(transaction, $"SELECT Source, ProviderID, Name, Description FROM {table}"))
        {
            var source = AsInt(row[0]);
            var id = row[1] as string ?? string.Empty;
            if (row[2] is string { } name && !string.IsNullOrWhiteSpace(name))
            {
                var title = new CopiedText(entityType, id, TitleLanguage.Unknown, "unk", null, name, 0, TitleType.Main);
                if (!isGeneric(source, title))
                    titles.Add((source, title));
            }

            if (row[3] is string { } description && !string.IsNullOrWhiteSpace(description))
                overviews.Add((source, new(entityType, id, TitleLanguage.Unknown, "unk", null, description, 0, TitleType.None)));
        }
    }

    /// <summary>
    ///   Inserts the texts of orderings and groups that are not stored yet,
    ///   each under its entry's own source.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="table">The title or overview table.</param>
    /// <param name="texts">The texts, with their source's number.</param>
    /// <returns>How many texts were inserted.</returns>
    private static int InsertNewOrderingTexts(DbTransaction transaction, string table, List<(int Source, CopiedText Text)> texts)
    {
        var ordering = KindNumber(MetadataEntityType.Ordering);
        var season = KindNumber(MetadataEntityType.Season);
        var stored = Read(
                transaction,
                $"SELECT EntitySource, EntityType, EntityID, Value FROM {table} WHERE EntityType IN ({ordering}, {season}) AND Source = EntitySource"
            )
            .Select(row => (AsInt(row[0]), AsInt(row[1]), row[2] as string ?? string.Empty, row[3] as string ?? string.Empty))
            .ToHashSet();
        var inserted = 0;
        foreach (var bySource in texts.GroupBy(text => text.Source))
        {
            var source = MetadataNumberRegistry.GetSource((byte)bySource.Key);
            var fresh = bySource
                .Select(text => text.Text)
                .Where(text => !stored.Contains((bySource.Key, KindNumber(text.EntityType), text.EntityID, text.Value)))
                .ToList();
            InsertTexts(transaction, table, fresh, source, source);
            inserted += fresh.Count;
        }

        return inserted;
    }

    #endregion
}
