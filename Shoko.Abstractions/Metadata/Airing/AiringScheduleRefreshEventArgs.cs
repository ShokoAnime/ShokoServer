using System;

namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Event arguments for one provider's finished refresh of one entry,
///   queued or waited on, whatever the outcome.
/// </summary>
public class AiringScheduleRefreshEventArgs : EventArgs
{
    /// <summary>
    ///   The entry that was refreshed: a series, season or episode.
    /// </summary>
    public required MetadataGuid EntityID { get; init; }

    /// <summary>
    ///   What the provider did.
    /// </summary>
    public required AiringScheduleProviderRefresh Refresh { get; init; }

    /// <summary>
    ///   When the refresh started, in UTC.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    ///   When the refresh ended, in UTC.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    ///   How long the refresh took.
    /// </summary>
    public TimeSpan Duration => CompletedAt - StartedAt;
}
