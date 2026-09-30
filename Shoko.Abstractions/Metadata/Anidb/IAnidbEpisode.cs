using System;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB episode.
/// </summary>
public interface IAnidbEpisode : IEpisode, IWithUpdateDate
{
    /// <summary>
    ///   The ID of the AniDB anime this belongs to, the same ID
    ///   <see cref="IEpisode.SeriesID"/> holds as text.
    /// </summary>
    int AnidbAnimeID { get; }

    MetadataGuid IEpisode.SeriesID { get => new(MetadataSource.AniDB, MetadataEntityType.Series, AnidbAnimeID.ToString()); }

    /// <summary>
    ///   The AniDB episode ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int AnidbID { get; }

    /// <summary>
    ///   The date of the episode's regular broadcast, for matching it against
    ///   other sources. <see cref="IEpisode.AirDate"/> keeps AniDB's own date.
    /// </summary>
    /// <remarks>
    ///   The same as <see cref="IEpisode.AirDate"/>, unless the anime's
    ///   description notes the episode was shown early (at an event, on a
    ///   stream or on one channel) and when the regular run started.
    /// </remarks>
    DateOnly? RegularAirDate { get => AirDate; }

    /// <summary>
    /// Get the anidb anime info for the episode, if available.
    /// </summary>
    new IAnidbAnime? Series { get; }
}
