using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   A stored entry that pins its default images.
/// </summary>
public interface IMetadataDefaultImageSource
{
    /// <summary>
    ///   The resource ID of the entry's default image of a type, on the
    ///   entry's own source.
    /// </summary>
    /// <param name="imageType">The type.</param>
    /// <returns>The resource ID, or <c>null</c> when none is pinned.</returns>
    string? GetDefaultResourceID(ImageEntityType imageType);
}
