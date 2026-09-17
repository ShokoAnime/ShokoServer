using System;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.API.SignalR.Models;

/// <summary>
/// One finished chunk of a core-driven sweep of an airing schedule provider.
/// </summary>
/// <remarks>
/// A sweep of a long run is many chunks, and one of these is sent per chunk, so
/// <c>IsFinished</c> is what says whether the provider is done or will carry on
/// in the next one. Nothing about a sweep is stored server-side, so a client
/// that wants a history keeps its own.
/// </remarks>
/// <param name="args">The event arguments.</param>
public class AiringSweepCompletedSignalRModel(AiringScheduleSweepEventArgs args)
{
    /// <summary>
    /// The ID of the provider that was swept.
    /// </summary>
    public Guid ProviderID { get; } = args.Provider.ID;

    /// <summary>
    /// The name of the provider that was swept.
    /// </summary>
    public string ProviderName { get; } = args.Provider.Name;

    /// <summary>
    /// How the chunk ended.
    /// </summary>
    public AiringScheduleSweepOutcome Outcome { get; } = args.Outcome;

    /// <summary>
    /// When the chunk started, in UTC.
    /// </summary>
    public DateTime StartedAt { get; } = args.StartedAt;

    /// <summary>
    /// When the chunk ended, in UTC.
    /// </summary>
    public DateTime CompletedAt { get; } = args.CompletedAt;

    /// <summary>
    /// How long the chunk took, in seconds.
    /// </summary>
    public double DurationSeconds { get; } = args.Duration.TotalSeconds;

    /// <summary>
    /// Whether the sweep is over, as opposed to resuming in a later chunk.
    /// </summary>
    public bool IsFinished { get; } = args.IsFinished;

    /// <summary>
    /// Why the sweep failed, or <c>null</c> when it did not.
    /// </summary>
    public string? ErrorMessage { get; } = args.ErrorMessage;
}
