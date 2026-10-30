using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Abstractions.Metadata.Storage;

/// <summary>
///   A studio to store.
/// </summary>
public sealed record MetadataStudioData
{
    /// <summary>
    ///   The studio: its source, the <c>studio</c> kind and the source's own
    ///   ID for it, e.g. <c>anilist://studio/7</c>.
    /// </summary>
    public required MetadataGuid ID { get; init; }

    /// <summary>
    ///   The studio's name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    ///   The studio's name in its original script, when it differs.
    /// </summary>
    public string? OriginalName { get; init; }

    /// <summary>
    ///   The country the studio originates from, usually an ISO 3166-1 code
    ///   such as <c>JP</c>, if the source says.
    /// </summary>
    public string? CountryOfOrigin { get; init; }

    /// <summary>
    ///   The source's resource ID of the studio's default image of each type,
    ///   which becomes its pinned default and is always the first downloaded
    ///   within the type's limit. <c>null</c> leaves the stored defaults as
    ///   they are, and an empty map clears them. A type the studio has no
    ///   images of is ignored.
    /// </summary>
    public IReadOnlyDictionary<ImageEntityType, string>? DefaultImageResourceIDs { get; init; }
}
