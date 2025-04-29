using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a video release is deleted.
/// </summary>
/// <param name="args">The event arguments.</param>
public class ReleaseDeletedSignalRModel(VideoReleaseDeletedEventArgs args)
{
    /// <summary>
    /// The video ID, if it was available when the release was deleted.
    /// </summary>
    public int? FileID { get; } = args.Video?.ID;

    /// <summary>
    /// The release info.
    /// </summary>
    public ReleaseInfoSignalRModel? Release { get; } = new(args.ReleaseInfo);
}
