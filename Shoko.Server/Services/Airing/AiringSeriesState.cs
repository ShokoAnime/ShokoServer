using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Anidb;

#nullable enable
namespace Shoko.Server.Services.Airing;

/// <summary>
/// What the collection filters of a read need to know about the series behind
/// an airing, resolved once per series and read.
/// </summary>
internal sealed class AiringSeriesState
{
    /// <summary>
    /// The state of an airing whose series resolves to nothing: not in the
    /// collection, not restricted, and nothing for a user to be refused.
    /// </summary>
    public static readonly AiringSeriesState Unknown = new();

    /// <summary>
    /// The series the airing's episode belongs to, or <c>null</c> when none
    /// could be resolved.
    /// </summary>
    public ISeries? Series { get; init; }

    /// <summary>
    /// The AniDB anime behind the series, which is what a user's restrictions
    /// are checked against, or <c>null</c> when there is none.
    /// </summary>
    public IAnidbAnime? AnidbAnime { get; init; }

    /// <summary>
    /// Whether the series is restricted (H).
    /// </summary>
    public bool IsRestricted { get; init; }

    /// <summary>
    /// Whether the series is in the collection, which means it has a shoko
    /// series.
    /// </summary>
    public bool IsInCollection { get; init; }

    /// <summary>
    /// Whether the series is in the collection with no local files.
    /// </summary>
    public bool IsMissing { get; init; }
}
