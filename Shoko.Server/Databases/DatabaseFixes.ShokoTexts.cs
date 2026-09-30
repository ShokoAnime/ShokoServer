using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Databases;

public partial class DatabaseFixes
{
    #region Shoko Texts | Steps

    /// <summary>
    ///   Copies the names and overviews users gave their series, episodes and
    ///   groups from <c>AnimeSeries.SeriesNameOverride</c>,
    ///   <c>AnimeEpisode.EpisodeNameOverride</c> and the manually named or
    ///   described <c>AnimeGroup</c> rows into <c>Metadata_Title</c> and
    ///   <c>Metadata_Overview</c>, as each entry's <c>user</c> text preferred
    ///   over every other.
    /// </summary>
    /// <remarks>
    ///   The texts are copied as they were typed, in no known language. A
    ///   blank name named nothing, so it is left out, but a blank group
    ///   overview hid the main series' one and is kept. Runs in one
    ///   transaction, and first removes what an earlier run of it wrote.
    /// </remarks>
    /// <param name="connection">The open connection to the database.</param>
    /// <returns>Whether it ran, and the error when it did not.</returns>
    public static Tuple<bool, string?> MigrateShokoTexts(object connection)
        => RunTextCopy(connection, "Shoko", "Metadata_Title and Metadata_Overview", (transaction, _) =>
        {
            var shoko = MetadataNumberRegistry.GetNumber(MetadataSource.Shoko);
            var user = MetadataNumberRegistry.GetNumber(MetadataSource.User);
            var overall = (int)TextPreference.Overall;
            var kinds = string.Join(", ", (int)MetadataNumberRegistry.GetNumber(MetadataEntityType.Series), (int)MetadataNumberRegistry.GetNumber(MetadataEntityType.Episode),
                (int)MetadataNumberRegistry.GetNumber(MetadataEntityType.Collection));
            foreach (var table in (string[])["Metadata_Title", "Metadata_Overview"])
                Execute(transaction,
                    $"DELETE FROM {table} WHERE EntitySource = {shoko} AND Source = {user} AND EntityType IN ({kinds}) AND Preference = {overall} AND ReferenceID IS NULL");

            var titles = new List<CopiedText>();
            titles.AddRange(ReadShokoTexts(transaction, MetadataEntityType.Series, "SELECT AnimeSeriesID, SeriesNameOverride FROM AnimeSeries WHERE SeriesNameOverride IS NOT NULL ORDER BY AnimeSeriesID", TitleType.Main, false));
            titles.AddRange(ReadShokoTexts(transaction, MetadataEntityType.Episode, "SELECT AnimeEpisodeID, EpisodeNameOverride FROM AnimeEpisode WHERE EpisodeNameOverride IS NOT NULL ORDER BY AnimeEpisodeID", TitleType.Main, false));
            titles.AddRange(ReadShokoTexts(transaction, MetadataEntityType.Collection, "SELECT AnimeGroupID, GroupName FROM AnimeGroup WHERE IsManuallyNamed = 1 ORDER BY AnimeGroupID", TitleType.Main, false));
            var overviews = ReadShokoTexts(transaction, MetadataEntityType.Collection, "SELECT AnimeGroupID, Description FROM AnimeGroup WHERE OverrideDescription = 1 ORDER BY AnimeGroupID", TitleType.None, true);

            InsertTexts(transaction, "Metadata_Title", titles, MetadataSource.Shoko, MetadataSource.User, TextPreference.Overall);
            InsertTexts(transaction, "Metadata_Overview", overviews, MetadataSource.Shoko, MetadataSource.User, TextPreference.Overall);
            return titles.Count + overviews.Count;
        });

    #endregion

    #region Shoko Texts | Helpers

    /// <summary>
    ///   Reads the texts users typed for one kind of Shoko entry.
    /// </summary>
    /// <param name="transaction">The open transaction.</param>
    /// <param name="entityType">The kind of entry.</param>
    /// <param name="sql">Selects each entry's ID and text.</param>
    /// <param name="titleType">The kind of title, or <see cref="TitleType.None"/> for overviews.</param>
    /// <param name="keepBlank">Whether a blank or missing text is copied as an empty one rather than left out.</param>
    /// <returns>The texts to copy.</returns>
    private static List<CopiedText> ReadShokoTexts(DbTransaction transaction, MetadataEntityType entityType, string sql, TitleType titleType, bool keepBlank)
    {
        var texts = new List<CopiedText>();
        foreach (var row in Read(transaction, sql))
        {
            var value = row[1] as string ?? string.Empty;
            if (!keepBlank && string.IsNullOrWhiteSpace(value))
                continue;

            texts.Add(new(
                entityType,
                Convert.ToInt32(row[0], CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
                TitleLanguage.Unknown,
                "unk",
                null,
                value,
                0,
                titleType
            ));
        }

        return texts;
    }

    #endregion
}
