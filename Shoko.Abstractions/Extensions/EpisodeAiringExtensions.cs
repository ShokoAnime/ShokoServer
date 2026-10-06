using System;
using Shoko.Abstractions.Metadata.Airing;

namespace Shoko.Abstractions.Extensions;

/// <summary>
/// Extensions for <see cref="IEpisodeAiring"/>.
/// </summary>
public static class EpisodeAiringExtensions
{
    /// <summary>
    /// Whether an airing is on air at a point in time: its slot has started
    /// and not yet ended. A date-only entry has no slot, so it never is.
    /// </summary>
    /// <param name="airing">The airing.</param>
    /// <param name="time">The point in time, in UTC.</param>
    /// <exception cref="ArgumentNullException"><paramref name="airing"/> is <c>null</c>.</exception>
    /// <returns><c>true</c> when the airing is on air then.</returns>
    public static bool IsAiringAt(this IEpisodeAiring airing, DateTime time)
    {
        ArgumentNullException.ThrowIfNull(airing);

        return airing.AiredAt is { } start && airing.EndsAt is { } end && start <= time && time < end;
    }
}
