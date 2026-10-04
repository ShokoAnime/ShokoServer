using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.Metadata;

/// <summary>
/// Series metadata.
/// </summary>
public interface ISeries : IWithTitles, IWithOverviews, IWithPrimaryImage, IWithLogoImage, IWithBackdropImage, IWithBannerImage, IWithDiscImage, IWithCastAndCrew, IWithStudios, IWithContentRatings, IWithYearlySeasons, IWithResources, IWithCrossSources, IWithTags, IWithCreationDate, IWithUpdateDate, IMetadata
{
    /// <summary>
    ///   When the core last refreshed the series in full from its source
    ///   without failing, whether or not anything changed, in UTC. Set by the
    ///   core alone; <c>null</c> when it never was.
    /// </summary>
    DateTime? LastRefreshedAt { get; }

    /// <summary>
    /// The shoko series ID, if we have any.
    /// </summary>
    IReadOnlyList<int> ShokoSeriesIDs { get; }

    /// <summary>
    /// The Anime Type.
    /// </summary>
    AnimeType Type { get; }

    /// <summary>
    /// The first aired date, if fully or partially known.
    /// </summary>
    PartialDateOnly? AirDate { get; }

    /// <summary>
    /// The end date of the series, if fully or partially known.
    /// </summary>
    PartialDateOnly? EndDate { get; }

    /// <summary>
    /// Overall user rating for the show, normalized on a scale of 1-10.
    /// </summary>
    double Rating { get; }

    /// <summary>
    /// The number of votes which were used to calculate the rating.
    /// </summary>
    int RatingVotes { get; }

    /// <summary>
    /// Indicates it's restricted for non-adult viewers. 😉
    /// </summary>
    bool Restricted { get; }

    /// <summary>
    /// Where the series is in its release.
    /// </summary>
    ReleaseStatus ReleaseStatus { get; }

    /// <summary>
    /// What the series was adapted from.
    /// </summary>
    SourceMaterial SourceMaterial { get; }

    /// <summary>
    /// The language the series was first made in, as a language code, when
    /// the source says.
    /// </summary>
    string? OriginalLanguageCode { get; }

    /// <summary>
    /// How popular the series is on its source, on the source's own scale,
    /// when the source measures it. Only comparable within one source.
    /// </summary>
    double? Popularity { get; }

    /// <summary>
    /// How many of the source's users marked the series a favorite, when the
    /// source counts them.
    /// </summary>
    int? FavoriteCount { get; }

    /// <summary>
    /// All shoko series linked to this entity.
    /// </summary>
    IReadOnlyList<IShokoSeries> ShokoSeries { get; }

    /// <summary>
    ///   The networks the series aired or streamed on, when the source lists
    ///   them.
    /// </summary>
    IReadOnlyList<INetwork> Networks { get; }

    /// <summary>
    ///   The countries the series was made in, as ISO 3166-1 codes when the
    ///   source gives them.
    /// </summary>
    IReadOnlyList<string> ProductionCountries { get; }

    /// <summary>
    /// Related series.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, ISeries>> RelatedSeries { get; }

    /// <summary>
    /// Related movies.
    /// </summary>
    IReadOnlyList<IRelatedMetadata<ISeries, IMovie>> RelatedMovies { get; }

    /// <summary>
    /// The series a provider's users suggest to someone looking at this one,
    /// best first within each source. Most of them are not in the collection,
    /// so their <see cref="ISuggestedMetadata.Suggested"/> is
    /// usually <c>null</c>.
    /// </summary>
    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> Suggestions { get; }

    /// <summary>
    /// The series in the collection that suggest this one, which is the same
    /// set read from the other end.
    /// </summary>
    IReadOnlyList<ISuggestedMetadata<ISeries, ISeries>> SuggestedBy { get; }

    /// <summary>
    /// All file-to-episode cross-references linked to the series.
    /// </summary>
    IReadOnlyList<IVideoCrossReference> VideoCrossReferences { get; }

    /// <summary>
    /// The series-level cross-references Shoko made that involve this entry:
    /// what it is linked to on the AniDB side, or which AniDB anime claim it
    /// on a provider's side. Not the file links in
    /// <see cref="VideoCrossReferences"/>.
    /// </summary>
    IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences { get; }

    /// <summary>
    /// The episode-level cross-references Shoko made for every episode of
    /// this entry, flattened across the series.
    /// </summary>
    IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences { get; }

    /// <summary>
    /// The cross-references Shoko made at the season level for this entry.
    /// </summary>
    /// <remarks>
    /// For the core sources, built from <see cref="MetadataEpisodeCrossReferences"/>
    /// rather than stored, so it only holds seasons some episode points into.
    /// </remarks>
    IReadOnlyList<IMetadataSeasonCrossReference> MetadataSeasonCrossReferences { get; }

    /// <summary>
    /// The film cross-references Shoko made for every episode of this entry:
    /// the anime is the film, kept against the episode standing for it. Empty
    /// on a provider's side, where a film sits in no series.
    /// </summary>
    IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences { get; }

    /// <summary>
    /// All known seasons for the series.
    /// </summary>
    IReadOnlyList<ISeason> Seasons { get; }

    /// <summary>
    ///   Every ordering of the series: the default one, made from its
    ///   <see cref="Seasons"/>, first, then the ones sources and users made.
    /// </summary>
    IReadOnlyList<IOrdering> Orderings { get; }

    /// <summary>
    ///   The ordering chosen for the series, or the default one when none is.
    /// </summary>
    IOrdering PreferredOrdering { get; }

    /// <summary>
    ///   The ordering the series is presented in. A source's own series
    ///   answers with its default ordering; the series an ordering's
    ///   <see cref="IOrdering.Series"/> gives answers with that ordering.
    /// </summary>
    IOrdering CurrentOrdering { get; }

    /// <summary>
    /// All known episodes for the series.
    /// </summary>
    IReadOnlyList<IEpisode> Episodes { get; }

    /// <summary>
    /// Get all videos linked to the series, if any.
    /// </summary>
    IReadOnlyList<IVideo> Videos { get; }

    /// <summary>
    /// The number of total episodes in the series.
    /// </summary>
    EpisodeCounts EpisodeCounts { get; }
}

/// <summary>
///   A series with its seasons, episodes and orderings typed.
/// </summary>
/// <typeparam name="TSeries">The series' type.</typeparam>
/// <typeparam name="TEpisode">The episodes' type.</typeparam>
public interface ISeries<out TSeries, out TEpisode> : ISeries
    where TSeries : class, ISeries
    where TEpisode : class, IEpisode
{
    /// <summary>
    ///   All known seasons for the series.
    /// </summary>
    new IReadOnlyList<ISeason<TSeries, TEpisode>> Seasons { get; }

    /// <summary>
    ///   All known episodes for the series.
    /// </summary>
    new IReadOnlyList<TEpisode> Episodes { get; }

    /// <summary>
    ///   Every ordering of the series: the default one, made from its
    ///   <see cref="Seasons"/>, first, then the ones sources and users made.
    /// </summary>
    new IReadOnlyList<IOrdering<TSeries, TEpisode>> Orderings { get; }

    /// <summary>
    ///   The ordering chosen for the series, or the default one when none is.
    /// </summary>
    new IOrdering<TSeries, TEpisode> PreferredOrdering { get; }

    /// <summary>
    ///   The ordering the series is presented in. A source's own series
    ///   answers with its default ordering.
    /// </summary>
    new IOrdering<TSeries, TEpisode> CurrentOrdering { get; }

    /// <summary>
    ///   The series a provider's users suggest to someone looking at this
    ///   one, best first within each source, with this series as the base.
    /// </summary>
    new IReadOnlyList<ISuggestedMetadata<TSeries, ISeries>> Suggestions { get; }

    /// <summary>
    ///   The series in the collection that suggest this one.
    /// </summary>
    new IReadOnlyList<ISuggestedMetadata<TSeries, ISeries>> SuggestedBy { get; }
}
