using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   How much to trust a match, as a band rather than a number.
/// </summary>
/// <remarks>
///   Finer than a user interface usually needs, so a caller can merge
///   neighbouring bands rather than being stuck when it wants to tell two of
///   them apart. Read off a <see cref="MatchRating"/>; nothing stores it.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MatchConfidence : byte
{
    /// <summary>
    ///   Nothing matched.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Nothing matched, and something was filled in to keep the run whole.
    /// </summary>
    Guessed = 1,

    /// <summary>
    ///   No evidence of its own; placed from what sits around it.
    /// </summary>
    Inferred = 2,

    /// <summary>
    ///   One signal, and only approximately.
    /// </summary>
    Weak = 3,

    /// <summary>
    ///   One signal, exactly.
    /// </summary>
    Probable = 4,

    /// <summary>
    ///   Two signals agreeing.
    /// </summary>
    Strong = 5,

    /// <summary>
    ///   A person said so.
    /// </summary>
    Verified = 6,
}
