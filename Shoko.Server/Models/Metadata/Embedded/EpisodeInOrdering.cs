using System;
using System.Collections.Generic;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.CrossReferences;
using Shoko.Abstractions.Metadata.Enums;
using Shoko.Abstractions.Metadata.Image.CrossReferences;
using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;
using Shoko.Server.Models.Interfaces;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in one ordering of its series, which is also the
///   episode as that ordering presents it: numbered and typed by the place,
///   with everything else, its ID included, from the linked episode.
/// </summary>
/// <remarks>
///   The place and the episode stay separate contracts. The place's untyped
///   <see cref="IEpisodeOrderingInformation.Episode"/> gives the linked
///   episode in the default ordering and this object in any other, so an
///   episode with no main title gets synthesized titles in that ordering's
///   numbering. The typed one always gives the linked episode.
/// </remarks>
public abstract class EpisodeInOrdering(IEpisode episode) : IEpisodeOrderingInformation, IEpisode, IInlineTextSource
{
    #region Place

    /// <inheritdoc />
    public abstract MetadataGuid OrderingID { get; }

    /// <inheritdoc />
    public abstract MetadataGuid? SeasonID { get; }

    /// <inheritdoc />
    public abstract int? SeasonNumber { get; }

    /// <inheritdoc />
    public abstract int EpisodeNumber { get; }

    /// <inheritdoc />
    public abstract EpisodeType EpisodeType { get; }

    /// <inheritdoc />
    public abstract bool IsDefault { get; }

    /// <inheritdoc />
    public abstract bool IsPreferred { get; }

    /// <inheritdoc />
    public abstract int? AirsBeforeSeasonNumber { get; }

    /// <inheritdoc />
    public abstract int? AirsBeforeEpisodeNumber { get; }

    /// <inheritdoc />
    public abstract int? AirsAfterSeasonNumber { get; }

    /// <inheritdoc />
    public abstract MetadataGuid? AirsAfterEpisodeID { get; }

    /// <inheritdoc />
    public abstract MetadataGuid? AirsBeforeEpisodeID { get; }

    /// <inheritdoc />
    public abstract ISeries Series { get; }

    /// <inheritdoc />
    public abstract ISeason? Season { get; }

    /// <inheritdoc />
    public abstract DateTime CreatedAt { get; }

    /// <inheritdoc />
    public abstract DateTime LastUpdatedAt { get; }

    /// <inheritdoc />
    public MetadataGuid SeriesID => episode.SeriesID;

    /// <inheritdoc />
    public MetadataGuid EpisodeID => episode.ID;

    /// <summary>
    ///   The linked episode, numbered by its own source.
    /// </summary>
    public IEpisode LinkedEpisode => episode;

    IEpisode IEpisodeOrderingInformation.Episode => IsDefault ? episode : this;

    /// <summary>
    ///   Whether the ordering numbers the episode other than its own source
    ///   does, by type or number.
    /// </summary>
    internal bool IsRenumbered => EpisodeType != episode.Type || EpisodeNumber != episode.EpisodeNumber;

    #endregion

    #region IEpisode Implementation

    /// <inheritdoc />
    public MetadataGuid ID => episode.ID;

    EpisodeType IEpisode.Type => EpisodeType;

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
    public DateOnly? EarlyAirDate => episode.EarlyAirDate;

    /// <inheritdoc />
    public DateTime? LastRefreshedAt => episode.LastRefreshedAt;

    IReadOnlyList<IEpisodeOrderingInformation> IEpisode.Orderings => episode.Orderings;

    IEpisodeOrderingInformation? IEpisode.PreferredOrdering => episode.PreferredOrdering;

    IEpisodeOrderingInformation IEpisode.CurrentOrdering => this;

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

    #region IWithTitles Implementation

    /// <inheritdoc />
    public string Title => (PreferredTitle ?? DefaultTitle).Value;

    /// <inheritdoc />
    public ITitle DefaultTitle => IsRenumbered ? TextAccess.Manager.DefaultTitleInOrdering(this) : episode.DefaultTitle;

    /// <inheritdoc />
    public ITitle? PreferredTitle => IsRenumbered ? TextAccess.Manager.PreferredTitleInOrdering(this) : episode.PreferredTitle;

    /// <inheritdoc />
    public IReadOnlyList<ITitle> Titles => IsRenumbered ? TextAccess.Manager.TitlesInOrdering(this) : episode.Titles;

    #endregion

    #region IWithOverviews Implementation

    /// <inheritdoc />
    public IText? DefaultOverview => episode.DefaultOverview;

    /// <inheritdoc />
    public IText? PreferredOverview => episode.PreferredOverview;

    /// <inheritdoc />
    public IReadOnlyList<IText> Overviews => episode.Overviews;

    #endregion

    #region Other Containers

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

    #endregion

    #region IInlineTextSource Implementation

    ITitle? IInlineTextSource.InlineTitle => (episode as IInlineTextSource)?.InlineTitle;

    IText? IInlineTextSource.InlineOverview => (episode as IInlineTextSource)?.InlineOverview;

    InlineTextPlacement IInlineTextSource.InlineTitlePlacement => (episode as IInlineTextSource)?.InlineTitlePlacement ?? InlineTextPlacement.First;

    InlineTextPlacement IInlineTextSource.InlineOverviewPlacement => (episode as IInlineTextSource)?.InlineOverviewPlacement ?? InlineTextPlacement.First;

    #endregion
}
