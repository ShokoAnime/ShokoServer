using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB anime.
/// </summary>
public interface IAnidbAnime : ISeries, IWithUpdateDate
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
    PartialDateOnly? RegularAirDate { get => AirDate; }

    /// <summary>
    /// All tags for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbTagForAnime> Tags { get; }

    IReadOnlyList<ITag> IWithTags.Tags { get => Tags; }

    /// <summary>
    /// The anime AniDB's users find similar to this one, best approved first.
    /// </summary>
    new IReadOnlyList<IAnidbSuggestion> Suggestions { get; }

    /// <summary>
    /// The anime AniDB's users find this one similar to.
    /// </summary>
    new IReadOnlyList<IAnidbSuggestion> SuggestedBy { get; }

    /// <summary>
    ///   All release group statuses for the AniDB anime.
    /// </summary>
    IReadOnlyList<IAnidbReleaseGroupStatus> ReleaseGroupStatuses { get; }

    /// <summary>
    /// All known fake "seasons" for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbSeason> Seasons { get; }

    /// <summary>
    /// All episodes for the AniDB anime.
    /// </summary>
    new IReadOnlyList<IAnidbEpisode> Episodes { get; }
}
