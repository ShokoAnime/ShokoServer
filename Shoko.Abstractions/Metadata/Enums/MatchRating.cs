
using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   How a match was arrived at.
/// </summary>
/// <remarks>
///   The values are storage ids, assigned as members were added and once
///   remapped, so they do not rank anything: a higher number is not a worse
///   match. To sort or group by how much to trust a match, use
///   <c>Score</c> or <c>Confidence</c>.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
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
    ///   The date matched and a title search agreed with it, the title being
    ///   close rather than exact. An upgrade of both
    ///   <see cref="DateMatches"/> and <see cref="TitleKindaMatches"/>, not a
    ///   weaker form of <see cref="DateAndTitleMatches"/>.
    /// </summary>
    DateAndTitleKindaMatches = 8,

    /// <summary>
    ///   No title or in-window date evidence; matched to the closest available air date as a last resort.
    /// </summary>
    DateKindaMatches = 9,

    /// <summary>
    ///   Date matches exactly and the episode numbers agree. Used by providers
    ///   without episode titles, where the number is only ever a corroborating
    ///   signal on top of a date match, never evidence on its own.
    /// </summary>
    DateAndNumberMatches = 10,

    /// <summary>
    ///   No date evidence of its own, but the neighbouring episodes that did
    ///   match by date all agree on the same episode-number offset, and this
    ///   episode sits on that offset.
    /// </summary>
    DateOffsetMatches = 11,
}
