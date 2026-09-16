using System;
using System.Collections.Generic;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// The knobs the delay inference needs that don't live on a single airing. Every
/// value has the default the service uses, so a caller only sets what differs.
/// </summary>
public sealed record AiringInferenceOptions
{
    /// <summary>
    /// How long a slotless airing is kept after the slot it lost before the
    /// owning provider's next write removes it.
    /// </summary>
    public static readonly TimeSpan DefaultSlotlessRetention = TimeSpan.FromDays(180);

    /// <summary>
    /// A move of at least this much is a postponement rather than a correction.
    /// </summary>
    public static readonly TimeSpan DefaultMoveThreshold = TimeSpan.FromHours(24);

    /// <summary>
    /// How far two moves may differ and still count as the same break.
    /// </summary>
    public static readonly TimeSpan DefaultCauseTolerance = TimeSpan.FromHours(1);

    /// <summary>
    /// Whether to infer delays at all. With it off, airings are stored exactly as
    /// submitted and every removed airing is deleted.
    /// </summary>
    public bool InferDelays { get; init; } = true;

    /// <summary>
    /// Whether the provider considers the run finished. A removed airing on a
    /// finished schedule is deleted rather than kept as a hiatus.
    /// </summary>
    public bool IsFinished { get; init; }

    /// <summary>
    /// Optional. The first episode the schedule covers. A removed airing outside
    /// the coverage is deleted rather than kept as a hiatus.
    /// </summary>
    public int? FirstEpisodeNumber { get; init; }

    /// <summary>
    /// Optional. The last episode the schedule covers.
    /// </summary>
    public int? LastEpisodeNumber { get; init; }

    /// <summary>
    /// How long a slotless airing is kept after the slot it lost.
    /// </summary>
    public TimeSpan SlotlessRetention { get; init; } = DefaultSlotlessRetention;

    /// <summary>
    /// A move of at least this much sets <see cref="InferredAiring.OriginalAiredAt"/>;
    /// anything smaller is a correction of the current slot.
    /// </summary>
    public TimeSpan MoveThreshold { get; init; } = DefaultMoveThreshold;

    /// <summary>
    /// How far two consecutive moves may differ and still count as one break, so
    /// only the first airing of the run is flagged as delayed.
    /// </summary>
    public TimeSpan CauseTolerance { get; init; } = DefaultCauseTolerance;

    /// <summary>
    /// Optional. The keys of the episodes another enabled provider already has a
    /// past airing for on the same channel with a matching track. A slotless
    /// airing for one of them is superseded, and is deleted by this write.
    /// </summary>
    public IReadOnlySet<string>? SupersededEpisodeKeys { get; init; }
}
