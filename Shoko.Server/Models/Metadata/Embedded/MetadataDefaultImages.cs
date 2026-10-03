using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Enums;

namespace Shoko.Server.Models.Metadata.Embedded;

/// <summary>
///   Writes the default images a save gives into an entry's extra data.
/// </summary>
internal static class MetadataDefaultImages
{
    /// <summary>
    ///   The longest resource ID the image table holds.
    /// </summary>
    private const int MaxResourceIDLength = 128;

    /// <summary>
    ///   An entry's extra data with the defaults a save gives, or as it was
    ///   when the save gives none.
    /// </summary>
    /// <typeparam name="TExtra">The kind of extra data.</typeparam>
    /// <param name="extra">The extra data stored now, or a new one.</param>
    /// <param name="resourceIDs">The defaults the save gives, or <c>null</c> to keep them.</param>
    /// <returns>The extra data.</returns>
    internal static TExtra Apply<TExtra>(TExtra extra, IReadOnlyDictionary<ImageEntityType, string>? resourceIDs)
        where TExtra : class, IMetadataDefaultImages<TExtra>
        => resourceIDs is null ? extra : extra.WithDefaultResourceIDs(resourceIDs);

    /// <summary>
    ///   The resource ID given for a type, unless it is blank or too long.
    /// </summary>
    /// <param name="resourceIDs">The resource IDs by type.</param>
    /// <param name="imageType">The type.</param>
    /// <returns>The trimmed resource ID, or <c>null</c>.</returns>
    internal static string? Of(IReadOnlyDictionary<ImageEntityType, string> resourceIDs, ImageEntityType imageType)
        => resourceIDs.TryGetValue(imageType, out var resourceID) && resourceID?.Trim() is { Length: > 0 and <= MaxResourceIDLength } trimmed ? trimmed : null;
}
