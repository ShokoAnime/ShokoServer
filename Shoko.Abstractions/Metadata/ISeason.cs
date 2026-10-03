using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Season Metadata.
/// </summary>
public interface ISeason : IWithTitles, IWithOverviews, IWithPrimaryImage, IWithLogoImage, IWithBackdropImage, IWithBannerImage, IWithDiscImage, IWithCastAndCrew, IWithYearlySeasons, IWithCreationDate, IWithUpdateDate, IMetadata
{
    /// <summary>
    ///   The series the season belongs to.
    /// </summary>
    MetadataGuid SeriesID { get; }

    /// <summary>
    ///   The ordering the season is a group of. The series' own seasons make
    ///   its default ordering, whose ID
    ///   <see cref="IOrdering.DefaultOrderingID"/> gives.
    /// </summary>
    MetadataGuid OrderingID { get => IOrdering.DefaultOrderingID(SeriesID); }

    /// <summary>
    ///   The season's number in its ordering.
    /// </summary>
    int SeasonNumber { get; }

    /// <summary>
    ///   Whether the season holds the specials, season <c>0</c>. An ordering
    ///   has at most one special group.
    /// </summary>
    bool IsSpecial { get => SeasonNumber == 0; }

    /// <summary>
    /// Get the series info for the season. A season always belongs to a
    /// series.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    ///   The season's episodes. For a regular group of an ordering, only the
    ///   episodes at home there: a placed special belongs to the special
    ///   group, and its place in a regular group only says where it airs.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    /// <summary>
    ///   When the core last refreshed the season's series in full from its
    ///   source, in UTC, see <see cref="ISeries.LastRefreshedAt"/>.
    /// </summary>
    DateTime? LastRefreshedAt { get => Series.LastRefreshedAt; }

    /// <summary>
    ///   The seasons of other sources the season's episodes are linked into,
    ///   read off <see cref="MetadataSeasonCrossReferences"/>. Empty on a
    ///   provider's side, where the links lead to AniDB anime.
    /// </summary>
    IReadOnlyList<ISeason> LinkedSeasons
    {
        get => [.. MetadataSeasonCrossReferences.Where(xref => xref.ProviderID != ID).Select(xref => xref.Provider).OfType<ISeason>()];
    }

    /// <summary>
    ///   The movies linked to the season's episodes, read off
    ///   <see cref="MetadataMovieCrossReferences"/>.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies
    {
        get => [.. MetadataMovieCrossReferences.Select(xref => xref.Provider).OfType<IMovie>()];
    }

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

/// <summary>
///   A season, or a group of an ordering, with its series, episodes and
///   ordering typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
public interface ISeason<out TSeries, out TEpisode> : ISeason
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   The series the season belongs to.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    new TSeries Series { get; }

    ISeries ISeason.Series { get => Series; }

    /// <summary>
    ///   The season's episodes. For a regular group of an ordering, only the
    ///   episodes at home there, placed specials left out.
    /// </summary>
    new IReadOnlyList<TEpisode> Episodes { get; }

    IReadOnlyList<IEpisode> ISeason.Episodes { get => Episodes; }

    /// <summary>
    ///   The ordering the season is a group of, see
    ///   <see cref="ISeason.OrderingID"/>.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    IOrdering<TSeries, TEpisode> Ordering { get; }
}
