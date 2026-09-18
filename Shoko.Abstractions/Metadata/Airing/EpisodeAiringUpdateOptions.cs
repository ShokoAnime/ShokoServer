namespace Shoko.Abstractions.Metadata.Airing;

/// <summary>
///   Options for how the service writes a schedule's airings.
/// </summary>
/// <remarks>
///   The coverage on this object is what the write judges a removal against,
///   and nothing more. It is never written back to the schedule, which only
///   <c>AddOrUpdateSchedule</c> and <c>UpdateSchedule</c> change, and it never
///   relaxes the validation an airing has to pass, which always runs against
///   what the schedule actually covers. A property left alone means the
///   write has no opinion and reads the schedule's own value, and a property
///   where <c>null</c> is itself a value carries a <c>Has…Set</c> flag, set by
///   its setter.
/// </remarks>
public sealed class EpisodeAiringUpdateOptions
{
    /// <summary>
    ///   Whether to infer delays while writing the airings. Turn it off for a
    ///   provider that reports delays itself; airings are then stored exactly
    ///   as submitted, and an airing the write takes away by leaving it out is
    ///   deleted rather than kept as a possible hiatus. It has no say over an
    ///   airing named for removal, which
    ///   <see cref="KeepRemovalsAsHiatus"/> decides under either setting.
    ///   Defaults to <c>true</c>.
    /// </summary>
    public bool InferDelays { get; init; } = true;

    /// <summary>
    ///   Whether an airing this write names for removal is kept, slotless, as a
    ///   hiatus instead of being deleted. It only has anything to say about the
    ///   airings handed to <c>MergeAirings</c> as removals: naming one deletes
    ///   it by default, exactly as <c>RemoveAiring</c> does, and an airing left
    ///   out of a <c>SetAirings</c> submission is a hiatus whatever this says.
    ///   Turn it on for a provider whose removal means "my source pre-empted
    ///   this" rather than "this row should go". Defaults to <c>false</c>.
    /// </summary>
    /// <remarks>
    ///   A hiatus is only ever kept for a slot that is still ahead of us and
    ///   inside what the write judges the run to be. One whose slot has passed,
    ///   one on a run this write calls finished, and one outside the stated
    ///   coverage is deleted as history with this on.
    /// </remarks>
    public bool KeepRemovalsAsHiatus { get; init; }

    /// <summary>
    ///   Whether the provider considers the run finished, for this write's
    ///   judgement of a removed airing: one on a finished run is history rather
    ///   than a hiatus. <c>null</c>, the default, reads the schedule's own
    ///   value.
    /// </summary>
    public bool? IsFinished { get; init; }

    /// <summary>
    ///   Used by the service to determine whether
    ///   <see cref="FirstEpisodeNumber"/> was stated. Set to <c>true</c> when
    ///   the property is set.
    /// </summary>
    public bool HasFirstEpisodeNumberSet { get; private set; }

    private int? _firstEpisodeNumber;

    /// <summary>
    ///   The first episode this write judges a removal against, in the series'
    ///   (or season's) numbering. Set it to <c>null</c> to state that the range
    ///   is open-ended at that end; leave it alone to read the schedule's own
    ///   value.
    /// </summary>
    public int? FirstEpisodeNumber
    {
        get => _firstEpisodeNumber;
        init
        {
            HasFirstEpisodeNumberSet = true;
            _firstEpisodeNumber = value;
        }
    }

    /// <summary>
    ///   Used by the service to determine whether
    ///   <see cref="LastEpisodeNumber"/> was stated. Set to <c>true</c> when the
    ///   property is set.
    /// </summary>
    public bool HasLastEpisodeNumberSet { get; private set; }

    private int? _lastEpisodeNumber;

    /// <summary>
    ///   The last episode this write judges a removal against, in the series'
    ///   (or season's) numbering. Set it to <c>null</c> to state that the range
    ///   is open-ended at that end; leave it alone to read the schedule's own
    ///   value.
    /// </summary>
    public int? LastEpisodeNumber
    {
        get => _lastEpisodeNumber;
        init
        {
            HasLastEpisodeNumberSet = true;
            _lastEpisodeNumber = value;
        }
    }
}
