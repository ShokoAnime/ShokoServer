using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Server.Repositories;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.CrossReference.Embedded;

/// <summary>
/// A season an anime covers, worked out from the episode links rather than
/// kept. Nothing points at a season directly, so there is no row to store: the
/// episodes that point into it are what say it is covered at all.
/// </summary>
public class MetadataSeasonCrossReference : IMetadataSeasonCrossReference
{
    /// <inheritdoc />
    public required MetadataSource Source { get; init; }

    /// <inheritdoc />
    public required int AnidbAnimeID { get; init; }

    /// <summary>
    ///   The season the episode links point into.
    /// </summary>
    public required MetadataGuid ProviderID { get; init; }

    /// <inheritdoc />
    public required MetadataGuid ProviderParentID { get; init; }

    /// <inheritdoc />
    public required int SeasonNumber { get; init; }

    /// <inheritdoc />
    public MatchRating MatchRating { get; init; }

    /// <inheritdoc />
    public int Ordering { get; init; }

    /// <inheritdoc />
    public MetadataEntityType EntityType => MetadataEntityType.Season;

    /// <inheritdoc />
    /// <remarks>
    ///   Nobody wrote it, since it was never written.
    /// </remarks>
    public Guid? WrittenBy => null;

    /// <inheritdoc />
    public IShokoSeries? ShokoSeries => RepoFactory.AnimeSeries.GetByAnimeID(AnidbAnimeID);

    /// <inheritdoc />
    public IMetadata? Provider
        => MetadataEntries.Resolve(ProviderID);

    /// <summary>
    ///   Works out the seasons a set of episode links point into, from the
    ///   season each link records. The season's number and series come from
    ///   the link, or from the series store when it holds the season; a link
    ///   whose season is not known, or whose number or series neither gives,
    ///   adds nothing.
    /// </summary>
    /// <param name="anidbAnimeID">The AniDB anime the links belong to.</param>
    /// <param name="links">The episode links to read the seasons off.</param>
    /// <param name="storedSeason">
    ///   Finds a season's number and series in the series store. Left out,
    ///   the store is read through <see cref="RepoFactory"/>.
    /// </param>
    /// <returns>One link per season reached, by source then season number.</returns>
    internal static List<MetadataSeasonCrossReference> Project(
        int anidbAnimeID,
        IEnumerable<IMetadataEpisodeCrossReference> links,
        Func<MetadataGuid, (int SeasonNumber, MetadataGuid SeriesID)?>? storedSeason = null
    )
    {
        storedSeason ??= StoredSeason;
        var seasons = new Dictionary<MetadataGuid, MetadataSeasonCrossReference>();
        foreach (var link in links)
        {
            if (link.SeasonID is not { } seasonID || seasons.ContainsKey(seasonID))
                continue;

            // What the link leaves out is read off the stored season, so a
            // link naming only its season still reaches it.
            var stored = link.SeasonNumber is null || link.ProviderParentID is null ? storedSeason(seasonID) : null;
            if ((link.SeasonNumber ?? stored?.SeasonNumber) is not { } seasonNumber || (link.ProviderParentID ?? stored?.SeriesID) is not { } seriesID)
                continue;

            // The first episode to reach a season speaks for it; the rest only
            // say the same thing again.
            seasons.TryAdd(seasonID, new()
            {
                Source = link.Source,
                AnidbAnimeID = anidbAnimeID,
                ProviderID = seasonID,
                ProviderParentID = seriesID,
                SeasonNumber = seasonNumber,
                MatchRating = link.MatchRating,
            });
        }

        return
        [
            .. seasons.Values
                .OrderBy(season => season.Source)
                .ThenBy(season => season.SeasonNumber)
                .Select((season, position) => new MetadataSeasonCrossReference
                {
                    Source = season.Source,
                    AnidbAnimeID = season.AnidbAnimeID,
                    ProviderID = season.ProviderID,
                    ProviderParentID = season.ProviderParentID,
                    SeasonNumber = season.SeasonNumber,
                    MatchRating = season.MatchRating,
                    Ordering = position,
                }),
        ];
    }

    /// <summary>
    ///   A plugin source's season's number and series, as the series store
    ///   holds them.
    /// </summary>
    /// <param name="seasonID">The season.</param>
    /// <returns>The number and series, or <c>null</c> when the store lacks the season.</returns>
    private static (int SeasonNumber, MetadataGuid SeriesID)? StoredSeason(MetadataGuid seasonID)
        => seasonID.Source.IsCore || RepoFactory.Metadata_Season?.GetByProviderID(seasonID.Source, seasonID.ID) is not ISeason season
            ? null
            : (season.SeasonNumber, season.SeriesID);
}
