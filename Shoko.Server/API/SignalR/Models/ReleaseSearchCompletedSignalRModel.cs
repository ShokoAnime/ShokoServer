
using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions.Events;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// Dispatched when a video release auto-search is completed.
/// </summary>
/// <param name="args">The event arguments.</param>
public class ReleaseSearchCompletedSignalRModel(VideoReleaseSearchCompletedEventArgs args)
{
    /// <summary>
    /// The video ID.
    /// </summary>
    public int FileID { get; } = args.Video.ID;

    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public bool IsSaved { get; } = args.IsSaved;

    /// <summary>
    /// Indicates if the search is/was automatic.
    /// </summary>
    public bool IsAutomatic { get; } = args.IsAutomatic;

    /// <summary>
    /// The IDs of the release providers that were attempted.
    /// </summary>
    public IReadOnlyList<Guid> AttemptedProviders { get; } = args.AttemptedProviders.Select(p => p.ID).ToList();

    /// <summary>
    /// The ID of the release provider that was selected for the successful
    /// search.
    /// </summary>
    public Guid? SelectedProvider { get; } = args.SelectedProvider?.ID;

    /// <summary>
    /// The found release info, if successful.
    /// </summary>
    public ReleaseInfoSignalRModel? ReleaseInfo { get; } = args.ReleaseInfo is not null ? new(args.ReleaseInfo) : null;

    /// <summary>
    /// The exception that occurred during the search, if any.
    /// </summary>
    public string? ExceptionMessage { get; } = args.Exception?.Message;

    /// <summary>
    /// The time the search started.
    /// </summary>
    public DateTime StartedAt { get; } = args.StartedAt.ToUniversalTime();

    /// <summary>
    /// The time the search completed.
    /// </summary>
    public DateTime CompletedAt { get; } = args.CompletedAt.ToUniversalTime();
}
