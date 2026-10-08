namespace Shoko.Abstractions.Connectivity.Suspensions;

/// <summary>
///   How a suspension ended.
/// </summary>
public enum SuspensionRemovalCause
{
    /// <summary>
    ///   Its <see cref="Suspension.ResumesAt"/> passed.
    /// </summary>
    Expired,

    /// <summary>
    ///   Its provider resumed it.
    /// </summary>
    Resumed,

    /// <summary>
    ///   An admin lifted it.
    /// </summary>
    Lifted,
}
