using Shoko.Abstractions.Metadata;

namespace Shoko.Server.Services;

/// <summary>
///   What a core source says about its own IDs, so the cross-reference store
///   refuses a link to an entry the source could never have given.
/// </summary>
/// <remarks>
///   Each core source registers at most one. A source with none takes any ID
///   a <see cref="MetadataGuid"/> can hold.
/// </remarks>
public interface IMetadataLinkIDRule
{
    /// <summary>
    ///   The source whose IDs the rule checks.
    /// </summary>
    MetadataSource Source { get; }

    /// <summary>
    ///   Checks whether an entry of the rule's source may be linked to.
    /// </summary>
    /// <param name="entry">An entry on <see cref="Source"/>, of any kind.</param>
    /// <returns><see langword="true"/> when the source could have given the entry its ID.</returns>
    bool IsValid(MetadataGuid entry);
}
