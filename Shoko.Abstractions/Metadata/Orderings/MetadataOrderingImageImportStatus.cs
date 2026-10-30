using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   What an ordering import did with one image of the payload.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImageImportStatus
{
    /// <summary>
    ///   Restored from the file in the payload.
    /// </summary>
    FromPayload = 0,

    /// <summary>
    ///   Restored from the remote source, and already held on this server.
    /// </summary>
    FromUrl = 1,

    /// <summary>
    ///   Linked from the remote source, with its download queued.
    /// </summary>
    Pending = 2,

    /// <summary>
    ///   Could not be restored; the reason says why.
    /// </summary>
    Failed = 3,

    /// <summary>
    ///   Not restored, because images were not asked for or the ordering was
    ///   skipped.
    /// </summary>
    Skipped = 4,
}
