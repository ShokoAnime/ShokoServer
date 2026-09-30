using System.Text.Json.Serialization;
using Newtonsoft.Json.Converters;

namespace Shoko.Abstractions.Metadata.Orderings;

/// <summary>
///   How an ordering export carries the images of the orderings and their
///   groups.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImageExportMode
{
    /// <summary>
    ///   No images.
    /// </summary>
    None = 0,

    /// <summary>
    ///   Each image by its remote source and URL only. An image with no
    ///   remote source, such as one a user uploaded, is left out.
    /// </summary>
    UrlOnly = 1,

    /// <summary>
    ///   Each image by its remote source and URL, and the file itself for an
    ///   image with no remote source.
    /// </summary>
    EmbedMissingRemote = 2,

    /// <summary>
    ///   The file of every image, with its remote source and URL when known.
    /// </summary>
    EmbedAll = 3,
}

/// <summary>
///   The file an ordering export is written as.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingContainer
{
    /// <summary>
    ///   A zip archive when image files are embedded, else plain JSON.
    /// </summary>
    Auto = 0,

    /// <summary>
    ///   Plain JSON, with any embedded image file inline as base64.
    /// </summary>
    Json = 1,

    /// <summary>
    ///   A zip archive holding <c>manifest.json</c> and each embedded image
    ///   file as <c>images/&lt;sha256&gt;.&lt;ext&gt;</c>.
    /// </summary>
    Zip = 2,
}

/// <summary>
///   What an ordering import does when the series already has a local
///   ordering with the same name, compared without regard to case.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingConflictMode
{
    /// <summary>
    ///   Leave the existing ordering alone and skip the imported one.
    /// </summary>
    Skip = 0,

    /// <summary>
    ///   Replace the existing ordering whole, groups and images, keeping its
    ///   ID and whether it is the series' chosen ordering.
    /// </summary>
    Replace = 1,

    /// <summary>
    ///   Keep both, giving the imported one a free name such as
    ///   <c>DVD Order (2)</c>.
    /// </summary>
    KeepBoth = 2,
}

/// <summary>
///   Where an ordering import restores each image from.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImageImportMode
{
    /// <summary>
    ///   The file in the payload, else the remote URL.
    /// </summary>
    PayloadFirst = 0,

    /// <summary>
    ///   The remote URL, else the file in the payload.
    /// </summary>
    UrlFirst = 1,

    /// <summary>
    ///   Only the file in the payload.
    /// </summary>
    PayloadOnly = 2,

    /// <summary>
    ///   Only the remote URL.
    /// </summary>
    UrlOnly = 3,

    /// <summary>
    ///   No images.
    /// </summary>
    None = 4,
}

/// <summary>
///   What an ordering import did with one ordering of the payload.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
[Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
public enum MetadataOrderingImportOutcome
{
    /// <summary>
    ///   A new local ordering was made, or would be on a dry run.
    /// </summary>
    Created = 0,

    /// <summary>
    ///   An existing local ordering was replaced, or would be on a dry run.
    /// </summary>
    Replaced = 1,

    /// <summary>
    ///   Nothing was written; the reason says why.
    /// </summary>
    Skipped = 2,
}

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
