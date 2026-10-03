using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   An entry's extra data, which pins the entry's default image of each
///   type by its own source's resource ID.
/// </summary>
/// <typeparam name="TSelf">The extra data's own type.</typeparam>
public interface IMetadataDefaultImages<TSelf> where TSelf : class, IMetadataDefaultImages<TSelf>
{
    /// <summary>
    ///   The resource ID of the entry's default image of a type.
    /// </summary>
    /// <param name="imageType">The type.</param>
    /// <returns>The resource ID, or <c>null</c> when none is pinned or the entry has no images of the type.</returns>
    string? GetDefaultResourceID(ImageEntityType imageType);

    /// <summary>
    ///   The extra data with the defaults of every type the entry has pinned
    ///   as given.
    /// </summary>
    /// <param name="resourceIDs">The resource IDs by type; a type left out has no default.</param>
    /// <returns>The extra data.</returns>
    TSelf WithDefaultResourceIDs(IReadOnlyDictionary<ImageEntityType, string> resourceIDs);
}
