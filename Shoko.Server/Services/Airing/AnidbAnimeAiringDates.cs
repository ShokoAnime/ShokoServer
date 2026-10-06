using System;
using System.Collections.Generic;
using System.Linq;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// The local dates of an AniDB anime's stored normal airings, the earliest
/// per episode, which place it in the yearly seasons where AniDB gives its
/// regular episodes no date or does not list them yet.
/// </summary>
/// <param name="Episodes">The earliest airing date by AniDB episode ID, for the airings matched to one.</param>
/// <param name="Unmatched">The earliest airing date of each episode matched to no AniDB episode, sorted.</param>
internal sealed record AnidbAnimeAiringDates(
    IReadOnlyDictionary<int, DateOnly> Episodes,
    IReadOnlyList<DateOnly> Unmatched
)
{
    /// <summary>
    /// Whether another set holds the same dates, so a rebuild can keep the
    /// old one and what was worked out from it.
    /// </summary>
    /// <param name="other">The other set.</param>
    /// <returns><c>true</c> when both hold the same dates.</returns>
    public bool HasSameDates(AnidbAnimeAiringDates other)
        => Episodes.Count == other.Episodes.Count &&
            Episodes.All(pair => other.Episodes.TryGetValue(pair.Key, out var date) && date == pair.Value) &&
            Unmatched.SequenceEqual(other.Unmatched);
}
