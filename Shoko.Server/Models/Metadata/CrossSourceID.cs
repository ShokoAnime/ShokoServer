using Shoko.Abstractions.Metadata;
using Shoko.Server.Services.MetadataStorage;

namespace Shoko.Server.Models.Metadata;

/// <summary>
///   Builds the IDs other sources gave an entry, from the members the entry
///   already has.
/// </summary>
internal static class CrossSourceID
{
    /// <summary>
    ///   The ID a source gave an entry, when it has one. The source is parsed
    ///   on every call, so a plugin registering it later is still found.
    /// </summary>
    /// <param name="source">The source's value, registered or not.</param>
    /// <param name="entityType">What kind of entry the ID names.</param>
    /// <param name="id">The source's own ID, as text.</param>
    /// <returns>The identifier, or <c>null</c> when the ID is empty or not valid.</returns>
    internal static MetadataGuid? For(string source, MetadataEntityType entityType, string? id)
        => string.IsNullOrEmpty(id) || id is "0" ? null : MetadataEntries.ToGuid(MetadataSource.Parse(source), entityType, id);

    /// <summary>
    ///   The ID a source gave an entry, when it has a positive one.
    /// </summary>
    /// <param name="source">The source's value, registered or not.</param>
    /// <param name="entityType">What kind of entry the ID names.</param>
    /// <param name="id">The source's own ID.</param>
    /// <returns>The identifier, or <c>null</c> when there is no positive ID.</returns>
    internal static MetadataGuid? For(string source, MetadataEntityType entityType, int? id)
        => id is > 0 ? For(source, entityType, id.Value.ToString()) : null;
}
