using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Episode metadata.
/// </summary>
public interface IEpisode : IWithTitles, IWithOverviews, IWithBackdropImage, IWithCastAndCrew, IWithResources, IWithCrossSources, IMetadata
{
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
    ///   that cannot be hidden leaves it <see langword="false"/>.
    /// </summary>
    bool IsHidden { get => false; }

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
    /// Get the series info for the episode, if available.
    /// </summary>
    ISeries? Series { get; }

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
