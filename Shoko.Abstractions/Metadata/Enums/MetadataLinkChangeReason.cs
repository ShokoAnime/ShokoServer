using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Enums;

/// <summary>
///   Why links were written.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataLinkChangeReason : byte
{
    /// <summary>
    ///   Anything else, such as a plugin writing to the link store, or a
    ///   provider's refresh dropping episodes a link named.
    /// </summary>
    Other = 0,

    /// <summary>
    ///   Somebody asked through the API.
    /// </summary>
    Manual = 1,

    /// <summary>
    ///   An automatic search linked what it took, or the episodes of a series
    ///   were matched again when it was refreshed.
    /// </summary>
    AutoLink = 2,

    /// <summary>
    ///   A search somebody asked for one anime replaced the links it had.
    /// </summary>
    ForcedResearch = 3,

    /// <summary>
    ///   Links were marked as trusted or not, through
    ///   <see cref="Services.IMetadataLinkingService.SetMatchRating"/>.
    /// </summary>
    Verify = 4,

    /// <summary>
    ///   The entries the links named were purged.
    /// </summary>
    Purge = 5,

    /// <summary>
    ///   A file of links was imported.
    /// </summary>
    Import = 6,

    /// <summary>
    ///   The Shoko series of the anime was deleted, taking its links along.
    /// </summary>
    SeriesDeleted = 7,
}
