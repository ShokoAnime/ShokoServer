using System;
using Shoko.Abstractions.Metadata.Airing;

#nullable enable
namespace Shoko.Server.Models.Airing;

/// <summary>
/// Where one sweeping provider's sweep has got to. One row per provider, and
/// the only thing that survives a restart about a sweep.
/// </summary>
/// <remarks>
/// This is operational state rather than history: it exists so the driver can
/// resume a sweep and decide when the next one is due, and it is overwritten
/// every chunk. What a given run did is dispatched as
/// <see cref="AiringScheduleSweepEventArgs"/> and logged, never stored.
/// </remarks>
public class AiringScheduleSweepState
{
    /// <summary>
    /// The longest cursor a provider may return. The airing tables already give
    /// names this much room, and a cursor is an identifier rather than a
    /// payload, so a longer one is a provider trying to keep its state in the
    /// wrong place.
    /// </summary>
    public const int MaxCursorLength = 512;

    #region Database Columns

    /// <summary>
    /// Local database ID.
    /// </summary>
    public int AiringScheduleSweepStateID { get; set; }

    /// <summary>
    /// The ID of the provider the row is for.
    /// </summary>
    public Guid ProviderID { get; set; }

    /// <summary>
    /// Where the next chunk resumes, exactly as the provider returned it, or
    /// <c>null</c> when no sweep is in progress. Opaque: nothing in the server
    /// ever reads into it.
    /// </summary>
    public string? Cursor { get; set; }

    /// <summary>
    /// When the last chunk finished, in UTC. With no <see cref="Cursor"/> this
    /// is also when the last whole sweep finished, which is what the next
    /// sweep's interval is counted from.
    /// </summary>
    public DateTime LastRunAt { get; set; }

    /// <summary>
    /// How the last chunk ended.
    /// </summary>
    public AiringScheduleSweepOutcome LastOutcome { get; set; }

    /// <summary>
    /// How many chunks in a row have ended without the walk moving on, reset
    /// the moment one does. A chunk whose deadline passed before the provider
    /// finished a single unit gets nowhere, and without counting them the
    /// driver would call such a provider again every tick for ever.
    /// </summary>
    public int NoProgressCount { get; set; }

    #endregion

    #region Computed Properties

    /// <summary>
    /// Whether a sweep is in progress, which is exactly whether there is a
    /// cursor to resume from.
    /// </summary>
    public bool IsSweeping => Cursor is not null;

    #endregion
}
