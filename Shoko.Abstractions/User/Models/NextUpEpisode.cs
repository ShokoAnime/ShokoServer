using Shoko.Abstractions.Metadata.Shoko;
using Shoko.Abstractions.Video;

namespace Shoko.Abstractions.User.Models;

/// <summary>
///   The episode a user should watch next in a series.
/// </summary>
public sealed record NextUpEpisode
{
    /// <summary>
    ///   The episode.
    /// </summary>
    public required IShokoEpisode Episode { get; init; }

    /// <summary>
    ///   The video the user left part-way through, when the episode is the
    ///   one they are watching, or <c>null</c>.
    /// </summary>
    public IVideo? Video { get; init; }
}
