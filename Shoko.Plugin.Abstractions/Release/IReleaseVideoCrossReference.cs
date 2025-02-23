
namespace Shoko.Plugin.Abstractions.Release;

/// <summary>
/// Video cross-reference included in an <see cref="IReleaseInfo"/>.
/// </summary>
public interface IReleaseVideoCrossReference
{
    /// <summary>
    /// AniDB episode ID.
    /// </summary>
    int AnidbEpisodeID { get; }

    /// <summary>
    /// AniDB anime ID, if known by the provider. Otherwise we'll fetch it
    /// later using the <see cref="AnidbEpisodeID"/>.
    /// </summary>
    int? AnidbAnimeID { get; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video starts covering in 
    /// the range [0, 99], but must be less than <see cref="PercentageEnd"/>.
    /// </summary>
    int PercentageStart { get; }

    /// <summary>
    /// Where in the <see cref="AnidbEpisodeID"/> the video stops covering in
    /// the range [1, 100], but must be greater than <see cref="PercentageStart"/>.
    /// </summary>
    int PercentageEnd { get; }
}
