using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Why a candidate a source offered was not the one taken.
/// </summary>
/// <remarks>
///   One reason per candidate: the one that decided it.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MatchRejectionReason : byte
{
    /// <summary>
    ///   Not rejected: the candidate is the one taken.
    /// </summary>
    None = 0,

    /// <summary>
    ///   It matched as well as the one taken, and lost on the order the source
    ///   offered them in, or it was a search candidate the core turned down
    ///   for an entry the anime's AniDB resources name, rated above it.
    /// </summary>
    Outranked = 1,

    /// <summary>
    ///   Its titles did not match, or matched less closely than the one
    ///   taken.
    /// </summary>
    TitleMismatch = 2,

    /// <summary>
    ///   Its titles matched as well as the one taken, but its dates did not.
    /// </summary>
    DateMismatch = 3,

    /// <summary>
    ///   It matched as well as the one taken, but its episode count was
    ///   further off.
    /// </summary>
    EpisodeCountMismatch = 4,

    /// <summary>
    ///   It is a kind of release the anime is not, such as a music video
    ///   offered for a series.
    /// </summary>
    TypeMismatch = 5,

    /// <summary>
    ///   It is marked as adult, and adult entries were not allowed.
    /// </summary>
    Restricted = 6,

    /// <summary>
    ///   Another anime already claims it. Set by a source that refuses such
    ///   an entry, never by the matching itself, which knows nothing of
    ///   existing links.
    /// </summary>
    ClaimedElsewhere = 7,

    /// <summary>
    ///   Its kind, series or film, may not be linked from its source, as the
    ///   admin turned it off. Set by the core when it applies an auto-link.
    /// </summary>
    KindDisabled = 8,

    /// <summary>
    ///   It names no entry that can be linked to the anime: one on another
    ///   source, of a kind that is neither a series nor a film, with an ID the
    ///   source never gives, or for another anime or an episode of one. Set by
    ///   the core when it applies an auto-link.
    /// </summary>
    InvalidID = 9,

    /// <summary>
    ///   It was not found by the search but listed beside it for context: a
    ///   link the anime or one of its prequels already has. Set by the core
    ///   for every <see cref="MetadataAutoLinkOrigin.CurrentLink"/> and
    ///   <see cref="MetadataAutoLinkOrigin.PrequelLink"/> candidate.
    /// </summary>
    ExistingLink = 10,

    /// <summary>
    ///   A <see cref="MetadataAutoLinkOrigin.AnidbResource"/> or
    ///   <see cref="MetadataAutoLinkOrigin.CrossSourceLink"/> hint the core did
    ///   not take: the search took it too or took something it cannot replace,
    ///   another hint was taken first, or the anime is linked on the source
    ///   already.
    /// </summary>
    HintNotNeeded = 11,

    /// <summary>
    ///   Anything else.
    /// </summary>
    Other = 255,
}
