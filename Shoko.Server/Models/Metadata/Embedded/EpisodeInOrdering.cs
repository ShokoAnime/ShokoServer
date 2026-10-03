using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode numbered as it sits in one ordering of its series, for the
///   matching engine, which numbers candidates by their season and episode.
///   Everything but the numbering is the episode's own.
/// </summary>
/// <param name="episode">The episode.</param>
/// <param name="place">Its place in the ordering.</param>
internal sealed class EpisodeInOrdering(IEpisode episode, IEpisodeOrderingInformation place) : IEpisode
{
    #region Numbering

    /// <inheritdoc />
    public MetadataGuid? SeasonID => place.SeasonID;

    /// <inheritdoc />
    public int? SeasonNumber => place.SeasonNumber;

    /// <inheritdoc />
    public int EpisodeNumber => place.EpisodeNumber;

    /// <inheritdoc />
    public EpisodeType Type => place.EpisodeType;

    /// <inheritdoc />
    public ISeason? Season => place.Season;

    #endregion

    #region IEpisode Implementation

    /// <inheritdoc />
    public MetadataGuid ID => episode.ID;

    /// <inheritdoc />
    public MetadataGuid SeriesID => episode.SeriesID;

    /// <inheritdoc />
    public IReadOnlyList<int> ShokoEpisodeIDs => episode.ShokoEpisodeIDs;

    /// <inheritdoc />
    public double Rating => episode.Rating;

    /// <inheritdoc />
    public int RatingVotes => episode.RatingVotes;

    /// <inheritdoc />
    public TimeSpan Runtime => episode.Runtime;

    /// <inheritdoc />
    public bool IsHidden => episode.IsHidden;

    /// <inheritdoc />
    public DateOnly? AirDate => episode.AirDate;

    /// <inheritdoc />
    public DateTime? AirDateWithTime => episode.AirDateWithTime;

    /// <inheritdoc />
    public ISeries Series => episode.Series;

    /// <inheritdoc />
    public IReadOnlyList<IEpisodeOrderingInformation> Orderings => episode.Orderings;

    /// <inheritdoc />
    public IEpisodeOrderingInformation? PreferredOrdering => episode.PreferredOrdering;

    /// <inheritdoc />
    public IReadOnlyList<IShokoEpisode> ShokoEpisodes => episode.ShokoEpisodes;

    /// <inheritdoc />
    public IReadOnlyList<IVideoCrossReference> VideoCrossReferences => episode.VideoCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataEpisodeCrossReference> MetadataEpisodeCrossReferences => episode.MetadataEpisodeCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataSeriesCrossReference> MetadataSeriesCrossReferences => episode.MetadataSeriesCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IMetadataMovieCrossReference> MetadataMovieCrossReferences => episode.MetadataMovieCrossReferences;

    /// <inheritdoc />
    public IReadOnlyList<IVideo> Videos => episode.Videos;

    #endregion

    #region Containers

    /// <inheritdoc />
    public string Title => episode.Title;

    /// <inheritdoc />
    public ITitle DefaultTitle => episode.DefaultTitle;

    /// <inheritdoc />
    public ITitle? PreferredTitle => episode.PreferredTitle;

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles => episode.Titles;

    /// <inheritdoc />
    public IText? DefaultOverview => episode.DefaultOverview;

    /// <inheritdoc />
    public IText? PreferredOverview => episode.PreferredOverview;

    /// <inheritdoc />
    public IReadOnlyList<IText> Overviews => episode.Overviews;

    /// <inheritdoc />
    public IImageCrossReference? DefaultBackdropImageCrossReference => episode.DefaultBackdropImageCrossReference;

    /// <inheritdoc />
    public IReadOnlyList<ICast> Cast => episode.Cast;

    /// <inheritdoc />
    public IReadOnlyList<ICrew> Crew => episode.Crew;

    /// <inheritdoc />
    public IReadOnlyList<Resource> Resources => episode.Resources;

    /// <inheritdoc />
    public IReadOnlyList<MetadataGuid> CrossSourceIDs => episode.CrossSourceIDs;

    /// <inheritdoc />
    public DateTime CreatedAt => episode.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => episode.LastUpdatedAt;

    /// <inheritdoc />
    public DateTime? LastRefreshedAt => episode.LastRefreshedAt;

    #endregion
}
