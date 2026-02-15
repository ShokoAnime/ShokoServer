
using System;
using Shoko.Abstractions.Events;

namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a video release auto-search is started.
/// </summary>
/// <param name="args">The event arguments.</param>
public class ReleaseSearchStartedSignalRModel(VideoReleaseSearchStartedEventArgs args)
{
    /// <summary>
    /// The video ID.
    /// </summary>
    public int FileID { get; } = args.Video.ID;

    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public bool ShouldSave { get; } = args.ShouldSave;

    /// <summary>
    /// Indicates if the search is automatic.
    /// </summary>
    public bool IsAutomatic { get; } = args.IsAutomatic;

    /// <summary>
    /// The time the search started.
    /// </summary>
    public DateTime StartedAt { get; } = args.StartedAt.ToUniversalTime();
}
