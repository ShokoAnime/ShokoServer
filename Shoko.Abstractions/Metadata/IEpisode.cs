using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Episode metadata.
/// </summary>
public interface IEpisode : IWithTitles, IWithOverviews, IWithBackdropImage, IWithCastAndCrew, IWithResources, IWithCrossSources, IWithCreationDate, IWithUpdateDate, IMetadata
{
    /// <summary>
    ///   When the core last refreshed the episode's series in full from its
    ///   source, in UTC, see <see cref="ISeries.LastRefreshedAt"/>.
    /// </summary>
    DateTime? LastRefreshedAt { get => Series.LastRefreshedAt; }

    /// <summary>
    ///   The series the episode belongs to.
    /// </summary>
    MetadataGuid SeriesID { get; }

    /// <summary>
    ///   The season the episode belongs to, or <c>null</c> when the source
    ///   puts it in none.
    /// </summary>
    MetadataGuid? SeasonID { get; }

    /// <summary>
    /// The shoko episode ID, if we have any.
    /// </summary>
    IReadOnlyList<int> ShokoEpisodeIDs { get; }

    /// <summary>
    /// The episode type.
    /// </summary>
    EpisodeType Type { get; }

    /// <summary>
    /// The episode number.
    /// </summary>
    int EpisodeNumber { get; }

    /// <summary>
    /// The season number, if applicable.
    /// </summary>
    int? SeasonNumber { get; }

    /// <summary>
    /// Overall user rating for the episode, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// The number of votes which were used to calculate the rating.
    /// </summary>
    int RatingVotes { get; }

    /// <summary>
    /// The runtime of the episode, as a time span.
    /// </summary>
    TimeSpan Runtime { get; }

    /// <summary>
    ///   Whether a user hid the episode. Set it through
    ///   <c>IMetadataOrderingService.SetEpisodeHidden</c>. An episode type
    ///   that cannot be hidden gives <c>false</c>.
    /// </summary>
    bool IsHidden { get; }

    /// <summary>
    /// The day the episode aired, if available. When a precise air time is
    /// known this is the UTC calendar day of <see cref="AirDateWithTime"/>, so
    /// the two never disagree. Broadcast schedules are served by
    /// <c>IAiringScheduleService</c> and never change this date.
    /// </summary>
    DateOnly? AirDate { get; }

    /// <summary>
    ///   The precise day and time the episode aired in UTC, if available.
    /// </summary>
    DateTime? AirDateWithTime { get; }

    /// <summary>
    /// Get the series info for the episode. An episode always belongs to a
    /// series.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    ISeries Series { get; }

    /// <summary>
    ///   The season the episode belongs to, see <see cref="SeasonID"/>, or
    ///   <c>null</c> when it is in none or the season is not available.
    /// </summary>
    ISeason? Season { get => SeasonID is { } seasonID ? Series.Seasons.FirstOrDefault(season => season.ID == seasonID) : null; }

    /// <summary>
    ///   Every place the episode has in its series' orderings: its place in
    ///   the default ordering first, then one for each group of another
    ///   ordering it is in.
    /// </summary>
    IReadOnlyList<IEpisodeOrderingInformation> Orderings { get; }

    /// <summary>
    ///   The episode's first place in the ordering chosen for its series, or
    ///   <c>null</c> when that ordering leaves it out.
    /// </summary>
    IEpisodeOrderingInformation? PreferredOrdering { get; }

    /// <summary>
    ///   The place the episode is presented in. A source's own episode
    ///   answers with its place in the default ordering; the episode a place
    ///   in another ordering gives through
    ///   <see cref="IEpisodeOrderingInformation.Episode"/> answers with that
    ///   place.
    /// </summary>
    IEpisodeOrderingInformation CurrentOrdering { get; }

    /// <summary>
    /// All shoko episodes linked to this episode.
    /// </summary>
    IReadOnlyList<IShokoEpisode> ShokoEpisodes { get; }

    /// <summary>
    /// All file-to-episode cross-references linked to the episode.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> VideoCrossReferences { get; }

    /// <summary>
    /// The episode-level cross-references Shoko made that involve this entry:
    /// what it is linked to on the AniDB side, or which AniDB episodes claim
    /// it on a provider's side. Not the file links in
    /// <see cref="VideoCrossReferences"/>.
    /// </summary>
    IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; }

    /// <summary>
    /// The series-level cross-references Shoko made for the series this
    /// episode belongs to, read without loading the series.
    /// </summary>
    IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences { get; }

    /// <summary>
    /// The film cross-references Shoko made for this entry: the anime is the
    /// film, kept against the episode standing for it. Empty on a provider's
    /// side, where a film claims no episode.
    /// </summary>
    IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; }

    /// <summary>
    /// Get all videos linked to the episode, if any.
    /// </summary>
    IReadOnlyList<IVideo> Videos { get; }
}

/// <summary>
///   An episode with its series, season and places in orderings typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
public interface IEpisode<out TSeries, out TEpisode> : IEpisode
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   The series the episode belongs to.
    /// </summary>
    /// <exception cref="NullReferenceException">The series is missing.</exception>
    new TSeries Series { get; }

    /// <summary>
    ///   The season the episode belongs to, or <c>null</c> when it is in none
    ///   or the season is not available.
    /// </summary>
    new ISeason<TSeries, TEpisode>? Season { get; }

    /// <summary>
    ///   Every place the episode has in its series' orderings: its place in
    ///   the default ordering first, then one for each group of another
    ///   ordering it is in.
    /// </summary>
    new IReadOnlyList<IEpisodeOrderingInformation<TSeries, TEpisode>> Orderings { get; }

    /// <summary>
    ///   The episode's first place in the ordering chosen for its series, or
    ///   <c>null</c> when that ordering leaves it out.
    /// </summary>
    new IEpisodeOrderingInformation<TSeries, TEpisode>? PreferredOrdering { get; }

    /// <summary>
    ///   The place the episode is presented in. A source's own episode
    ///   answers with its place in the default ordering.
    /// </summary>
    new IEpisodeOrderingInformation<TSeries, TEpisode> CurrentOrdering { get; }
}
