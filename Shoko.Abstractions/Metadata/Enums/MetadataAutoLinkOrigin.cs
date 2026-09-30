using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Where an auto-link candidate came from, which decides whether an
///   automatic search may take it.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataAutoLinkOrigin : byte
{
    /// <summary>
    ///   Found and rated by the search. Linked automatically when taken.
    /// </summary>
    Search = 0,

    /// <summary>
    ///   A link the anime already has, listed for context and never linked
    ///   again by the search.
    /// </summary>
    CurrentLink = 1,

    /// <summary>
    ///   A link one of the anime's prequels has, listed for context and never
    ///   linked by the search.
    /// </summary>
    PrequelLink = 2,

    /// <summary>
    ///   An entry the anime's AniDB resources name on the source. Taken (never
    ///   as <see cref="MatchRating.UserVerified"/>) only while the anime has no
    ///   link on the source, or the search replaces them, and the search took
    ///   nothing or only competing picks rated below it, which it replaces. One
    ///   for the whole anime competes with every pick, a film's with those for
    ///   its episode or the whole anime.
    /// </summary>
    AnidbResource = 3,

    /// <summary>
    ///   An entry named by the cross-source IDs of what the anime is linked to
    ///   on another source (see <see cref="Services.IMetadataLinkingService.GetCrossSourceHints"/>).
    ///   Taken by the same rules as <see cref="AnidbResource"/>; one hint at
    ///   most is taken, of either origin.
    /// </summary>
    CrossSourceLink = 4,
}
