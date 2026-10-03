using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Services;
using Shoko.Server.API.v3.Models.Common;

namespace Shoko.Server.API.v3.Helpers;

/// <summary>
/// Builds the <c>Sources</c> block of a series or episode response: for each
/// plugin source asked for in <c>includeDataFrom</c>, a generic view of the
/// entries the series or episode is linked to there.
/// </summary>
internal static class LinkedMetadataHelper
{
    #region Sources

    /// <summary>
    /// The sources asked for that get a generic block: the plugins' sources.
    /// AniDB and TMDB keep blocks of their own, TMDB whether the core serves
    /// it or not, and the other core sources hold no linked entries, so they
    /// are left out as they always were.
    /// </summary>
    /// <param name="includeDataFrom">The sources asked for, if any.</param>
    /// <returns>The sources, ordered by value.</returns>
    public static IReadOnlyList<MetadataSource> GenericSources(IReadOnlySet<MetadataSource>? includeDataFrom)
        => includeDataFrom is not { Count: > 0 }
            ? []
            : [.. includeDataFrom
                .Where(source => !source.IsCore && source != MetadataSource.TMDB)
                .OrderBy(source => source.Value, StringComparer.Ordinal)];

    #endregion

    #region Blocks

    /// <summary>
    /// The series and movies an anime is linked to on each source.
    /// </summary>
    /// <param name="metadataService">Reads the links and the linked entries.</param>
    /// <param name="anidbAnimeID">The AniDB anime.</param>
    /// <param name="sources">The sources, from <see cref="GenericSources"/>.</param>
    /// <returns>
    /// One block per source, empty where nothing is linked; a link made to
    /// nothing, or to an entry the core has not stored, is left out.
    /// </returns>
    public static Dictionary<MetadataSource, LinkedSeriesMetadata> ForSeries(IMetadataService metadataService, int anidbAnimeID, IReadOnlyList<MetadataSource> sources)
    {
        var blocks = new Dictionary<MetadataSource, LinkedSeriesMetadata>();
        foreach (var source in sources)
        {
            var entries = Resolve(metadataService, [
                .. metadataService.GetSeriesCrossReferences(anidbAnimeID, source),
                .. metadataService.GetMovieCrossReferencesForSeries(anidbAnimeID, source),
            ]);
            blocks[source] = new()
            {
                Series = [.. entries.OfType<ISeries>().Select(series => new LinkedMetadataEntry(series))],
                Movies = [.. entries.OfType<IMovie>().Select(movie => new LinkedMetadataEntry(movie))],
            };
        }

        return blocks;
    }

    /// <summary>
    /// The episodes and movies an AniDB episode is linked to on each source.
    /// </summary>
    /// <param name="metadataService">Reads the links and the linked entries.</param>
    /// <param name="anidbEpisodeID">The AniDB episode.</param>
    /// <param name="sources">The sources, from <see cref="GenericSources"/>.</param>
    /// <returns>
    /// One block per source, empty where nothing is linked; a link made to
    /// nothing, or to an entry the core has not stored, is left out.
    /// </returns>
    public static Dictionary<MetadataSource, LinkedEpisodeMetadata> ForEpisode(IMetadataService metadataService, int anidbEpisodeID, IReadOnlyList<MetadataSource> sources)
    {
        var blocks = new Dictionary<MetadataSource, LinkedEpisodeMetadata>();
        foreach (var source in sources)
        {
            var entries = Resolve(metadataService, [
                .. metadataService.GetEpisodeCrossReferences(anidbEpisodeID, source),
                .. metadataService.GetMovieCrossReferences(anidbEpisodeID, source),
            ]);
            blocks[source] = new()
            {
                Episodes = [.. entries.OfType<IEpisode>().Select(episode => new LinkedMetadataEntry(episode))],
                Movies = [.. entries.OfType<IMovie>().Select(movie => new LinkedMetadataEntry(movie))],
            };
        }

        return blocks;
    }

    /// <summary>
    /// The stored entries a set of links names, once each, in link order.
    /// </summary>
    /// <param name="metadataService">Reads the entries.</param>
    /// <param name="links">The links.</param>
    /// <returns>The entries.</returns>
    private static List<IMetadata> Resolve(IMetadataService metadataService, IEnumerable<IMetadataCrossReference> links)
        => [.. links
            .Select(link => link.ProviderID)
            .OfType<MetadataGuid>()
            .Distinct()
            .Select(metadataService.GetEntry)
            .OfType<IMetadata>()];

    #endregion
}
