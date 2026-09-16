using System;

namespace Shoko.Server.Utilities.Airing;

/// <summary>
/// The limits the cadence helper measures within, so an unusual but ordinary run
/// isn't read as a break.
/// </summary>
public sealed record AiringCadenceOptions
{
    /// <summary>
    /// How many releases a line needs before it has a cadence at all. A season
    /// released at once is one release, so it never has one.
    /// </summary>
    public int MinimumReleases { get; init; } = 4;

    /// <summary>
    /// A gap this long or longer is a split cour rather than a break, and counts
    /// neither as a break nor towards the cadence.
    /// </summary>
    public TimeSpan MaximumGap { get; init; } = TimeSpan.FromDays(56);

    /// <summary>
    /// How far a gap may fall short of a whole skipped slot and still be read as
    /// one.
    /// </summary>
    public TimeSpan Tolerance { get; init; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Optional. The last episode the schedule covers. Airings past it are not
    /// measured.
    /// </summary>
    public int? LastEpisodeNumber { get; init; }
}
