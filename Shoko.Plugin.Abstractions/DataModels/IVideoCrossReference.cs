using Shoko.Plugin.Abstractions.DataModels.Shoko;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.DataModels;

/// <summary>
/// Video cross-reference.
/// </summary>
public interface IVideoCrossReference : IReleaseVideoCrossReference
{
    /// <summary>
    /// Source source of the cross-reference.
    /// </summary>
    public string Source { get; }

    /// <summary>
    /// ED2K hash used for video identification.
    /// </summary>
    string ED2K { get; }

    /// <summary>
    /// File size used for video identification.
    /// </summary>
    long Size { get; }

    /// <summary>
    /// AniDB episode ID. Will be available even if <see cref="AnidbEpisode"/>
    /// is not available yet.
    /// </summary>
    new int AnidbEpisodeID { get; }

    /// <summary>
    /// AniDB anime ID. Will be available even if <see cref="AnidbAnime"/> is
    /// not available yet.
    /// </summary>
    new int AnidbAnimeID { get; }

    /// <summary>
    /// Cross-reference percentage range, if the video covers less than 100%
    /// of the episode, then this field tells roughly how much it covers.
    /// </summary>
    int Percentage { get; }

    /// <summary>
    /// The local video, if available.
    /// </summary>
    IVideo? Video { get; }

    /// <summary>
    /// The <see cref="IReleaseInfo"/> associated with this
    /// <see cref="IVideoCrossReference"/>.
    /// </summary>
    IReleaseInfo Release { get; }

    /// <summary>
    /// The AniDB episode, if available.
    /// </summary>
    IEpisode? AnidbEpisode { get; }

    /// <summary>
    /// The AniDB anime series, if available.
    /// </summary>
    ISeries? AnidbAnime { get; }

    /// <summary>
    /// The Shoko episode, if available.
    /// </summary>
    IShokoEpisode? ShokoEpisode { get; }

    /// <summary>
    /// The Shoko series, if available.
    /// </summary>
    IShokoSeries? ShokoSeries { get; }
}
