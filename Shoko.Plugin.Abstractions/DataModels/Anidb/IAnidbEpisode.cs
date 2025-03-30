
namespace Shoko.Plugin.Abstractions.DataModels.Anidb;

/// <summary>
/// An AniDB episode.
/// </summary>
public interface IAnidbEpisode : IEpisode
{
    /// <summary>
    /// Get the anidb anime info for the episode, if available.
    /// </summary>
    new IAnidbAnime? Series { get; }
}
