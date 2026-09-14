using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anilist;

/// <summary>
/// An AniList episode.
/// </summary>
public interface IAnilistEpisode : IEpisode, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Get the AniList anime info for the "season," if available.
    /// </summary>
    new IAnilistAnime Series { get; }
}
