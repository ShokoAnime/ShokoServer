using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.User.Models;

/// <summary>
///   The episode a user is watching in a series: the one whose video they
///   last left part-way through.
/// </summary>
public sealed record ActiveEpisode
{
    /// <summary>
    ///   The episode.
    /// </summary>
    public required IShokoEpisode Episode { get; init; }

    /// <summary>
    ///   The video the user left part-way through.
    /// </summary>
    public required IVideo Video { get; init; }

    /// <summary>
    ///   The user's data for the video, with where they left it.
    /// </summary>
    public required IVideoUserData VideoUserData { get; init; }
}
