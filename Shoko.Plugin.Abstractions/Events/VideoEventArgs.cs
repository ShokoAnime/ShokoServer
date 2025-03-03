
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Base class for video events.
/// </summary>
public class VideoEventArgs
{
    /// <summary>
    /// The video.
    /// </summary>
    public required IVideo Video { get; init; }
}
