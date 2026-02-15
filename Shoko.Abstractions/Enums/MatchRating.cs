
namespace Shoko.Abstractions.Enums;

/// <summary>
///   Match rating.
/// </summary>
public enum MatchRating : byte
{
    /// <summary>
    ///   Nothing matched.
    /// </summary>
    None = 0,

    /// <summary>
    ///   The match has been verified by a user.
    /// </summary>
    UserVerified = 1,

    /// <summary>
    ///   Date and title match exactly.
    /// </summary>
    DateAndTitleMatches = 2,

    /// <summary>
    ///   Date matches exactly.
    /// </summary>
    DateMatches = 3,

    /// <summary>
    ///   Title matches exactly.
    /// </summary>
    TitleMatches = 4,

    /// <summary>
    ///   Neither the titles nor the dates matched, but we could fill from
    ///   adjacent data.
    /// </summary>
    FirstAvailable = 5,

    // 6 was remapped to 0.

    /// <summary>
    ///   Title is close, but not exact.
    /// </summary>
    TitleKindaMatches = 7,

    /// <summary>
    ///   Date and Title are close, but not exact.
    /// </summary>
    DateAndTitleKindaMatches = 8,
}
