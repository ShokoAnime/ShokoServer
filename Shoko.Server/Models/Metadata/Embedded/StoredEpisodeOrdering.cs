using System;
using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An episode's place in a group of a stored ordering.
/// </summary>
/// <param name="group">The group the episode is in.</param>
/// <param name="episode">The episode.</param>
/// <param name="position">The episode's place in the group, from <c>0</c>.</param>
public sealed class StoredEpisodeOrdering(StoredOrderingGroup group, IEpisode episode, int position) : IEpisodeOrderingInformation
{
    #region IEpisodeOrderingInformation Implementation

    /// <inheritdoc />
    public MetadataGuid SeriesID => group.SeriesID;

    /// <inheritdoc />
    public MetadataGuid OrderingID => group.Ordering.ID;

    /// <inheritdoc />
    public MetadataGuid? SeasonID => group.ID;

    /// <inheritdoc />
    public MetadataGuid EpisodeID => episode.ID;

    /// <inheritdoc />
    public int? SeasonNumber => group.SeasonNumber;

    /// <inheritdoc />
    public int EpisodeNumber => position + 1;

    /// <inheritdoc />
    public EpisodeType EpisodeType => group.IsSpecial ? EpisodeType.Special : EpisodeType.Episode;

    /// <inheritdoc />
    public bool IsDefault => false;

    /// <inheritdoc />
    public bool IsPreferred => group.Ordering.IsPreferred;

    /// <inheritdoc />
    public ISeries Series => group.Series;

    /// <inheritdoc />
    public ISeason? Season => group;

    /// <inheritdoc />
    public IEpisode Episode => episode;

    #endregion

    #region Dates

    /// <inheritdoc />
    public DateTime CreatedAt => group.Ordering.CreatedAt;

    /// <inheritdoc />
    public DateTime LastUpdatedAt => group.Ordering.LastUpdatedAt;

    #endregion
}
