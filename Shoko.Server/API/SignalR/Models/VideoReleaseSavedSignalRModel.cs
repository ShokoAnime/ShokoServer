using Shoko.Abstractions.Video.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a video release is saved.
/// </summary>
/// <param name="args">The event arguments.</param>
public class VideoReleaseSavedSignalRModel(VideoReleaseSavedEventArgs args)
{
    /// <summary>
    /// The video ID.
    /// </summary>
    public int FileID { get; } = args.Video.ID;

    /// <summary>
    /// The release info.
    /// </summary>
    public ReleaseInfoSignalRModel? Release { get; } = new(args.ReleaseInfo);
}
