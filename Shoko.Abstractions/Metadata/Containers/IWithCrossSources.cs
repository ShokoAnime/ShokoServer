using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity that carries the IDs other sources gave it.
/// </summary>
public interface IWithCrossSources
{
    /// <summary>
    ///   The IDs other sources gave this same entry, as far as the entry
    ///   itself carries them, such as the AniDB entry a Shoko entry is made
    ///   from. A source in it may be one nobody registered.
    /// </summary>
    /// <remarks>
    ///   Only the entry's own members feed it. The links Shoko made are the
    ///   entry's metadata cross-references.
    /// </remarks>
    IReadOnlyList<MetadataGuid> CrossSourceIDs { get; }
}
