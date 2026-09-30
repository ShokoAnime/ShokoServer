using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Season Metadata.
/// </summary>
public interface ISeason : IWithTitles, IWithOverviews, IWithPrimaryImage, IWithLogoImage, IWithBackdropImage, IWithBannerImage, IWithDiscImage, IWithCastAndCrew, IWithYearlySeasons, IMetadata
{
    /// <summary>
    ///   The series the season belongs to.
    /// </summary>
    MetadataGuid SeriesID { get; }

    /// <summary>
    ///   The season number. For one of the series' own seasons this is its
    ///   number in the default ordering. For a group of an ordering (when
    ///   <see cref="OrderingID"/> is set) stored by the core it is <c>0</c>
    ///   for the special group and the others' place among themselves from
    ///   1; a core source's own orderings keep the source's numbers.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    ///   Whether the season holds the specials, season <c>0</c>. An ordering
    ///   has at most one special group.
    /// </summary>
    bool IsSpecial { get => SeasonNumber == 0; }

    /// <summary>
    ///   The ordering the season is a group of, or <c>null</c> for one of
    ///   the series' own seasons, which make its default ordering.
    /// </summary>
    MetadataGuid? OrderingID { get => null; }

    /// <summary>
    /// Get the series info for the season. A season always belongs to a
    /// series.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    /// All episodes for the season.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    /// <summary>
    /// The season-level cross-references Shoko made that involve this entry:
    /// the seasons its episodes are linked into on the AniDB side, or which
    /// AniDB anime cover it on a provider's side.
    /// </summary>
    /// <remarks>
    /// Built from <see cref="MetadataEpisodeCrossReferences"/> rather than
    /// stored, so it only holds seasons some episode points into.
    /// </remarks>
    IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences { get; }

    /// <summary>
    /// The episode-level cross-references Shoko made for every episode of
    /// this entry, flattened across the season.
    /// </summary>
    IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; }

    /// <summary>
    /// The film cross-references Shoko made for the episodes of this entry:
    /// the anime is the film, kept against the episode standing for it. Empty
    /// on a provider's side, where a film sits in no season.
    /// </summary>
    IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; }
}
