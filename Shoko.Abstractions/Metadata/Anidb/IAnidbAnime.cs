using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries<IAnidbAnime, IAnidbEpisode>
{
    /// <summary>
    ///   The AniDB anime ID, the same ID <see cref="IMetadata.ID"/> holds as
    ///   text.
    /// </summary>
    int AnidbID { get; }

    /// <summary>
    /// My Anime List (MAL) IDs linked to the AniDB anime.
    /// </summary>
    IReadOnlyList<int> MalIDs { get; }

    /// <summary>
    ///   When the anime's regular broadcast started, for matching it against
    ///   other sources. The same as <see cref="ISeries.AirDate"/>, unless the
    ///   first episode was shown early and its
    ///   <see cref="IAnidbEpisode.RegularAirDate"/> falls after it, which it
    ///   then is. <see cref="ISeries.AirDate"/> keeps AniDB's own date.
    /// </summary>
    PartialDateOnly? RegularAirDate { get; }

    /// <summary>
    /// All tags for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbTagForAnime> Tags { get; }

    IReadOnlyList<ITag> IWithTags.Tags { get => Tags; }

    /// <summary>
    ///   All release group statuses for the AniDB anime.
    /// </summary>
    IReadOnlyList<IAnidbReleaseGroupStatus> ReleaseGroupStatuses { get; }
}
