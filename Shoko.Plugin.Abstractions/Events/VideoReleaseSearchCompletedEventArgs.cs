
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Release;

namespace Shoko.Plugin.Abstractions.Events;

/// <summary>
/// Dispatched when a video release search is completed.
/// </summary>
public class VideoReleaseSearchCompletedEventArgs : VideoEventArgs
{
    /// <summary>
    /// Indicates if the found releases should be saved.
    /// </summary>
    public required bool IsSaved { get; init; }

    /// <summary>
    /// Indicates if the search was successful.
    /// </summary>
    [MemberNotNullWhen(true, nameof(ReleaseInfo))]
    [MemberNotNullWhen(true, nameof(ProviderID))]
    public bool IsSuccessful => ReleaseInfo is not null && ProviderID is not { Length: > 0 };

    /// <summary>
    /// The IDs of the release providers that were attempted.
    /// </summary>
    public required IReadOnlyList<string> AttemptedProviderIDs { get; init; }

    /// <summary>
    /// The ID of the release provider that was selected for the successful
    /// search.
    /// </summary>
    public required string? ProviderID { get; init; }
    /// <summary>
    /// The found release info, if successful.
    /// </summary>
    public required IReleaseInfo? ReleaseInfo { get; init; }

    /// <summary>
    /// The exception that occurred during the search, if any.
    /// </summary>
    public required Exception? Exception { get; init; }

    /// <summary>
    /// The time the search started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// The time the search completed.
    /// </summary>
    public required DateTime CompletedAt { get; init; }
}
