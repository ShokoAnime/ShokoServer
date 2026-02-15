using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Anidb;

/// <summary>
/// An AniDB episode.
/// </summary>
public interface IAnidbEpisode : IEpisode, IWithUpdateDate
{
    /// <summary>
    /// Get the anidb anime info for the episode, if available.
    /// </summary>
    new IAnidbAnime? Series { get; }
}
