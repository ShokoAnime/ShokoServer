
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Base class for video events.
/// </summary>
/// <param name="video">The video.</param>
public class VideoEventArgs(IVideo video)
{
    /// <summary>
    /// The video.
    /// </summary>
    public IVideo Video { get; } = video;
}
