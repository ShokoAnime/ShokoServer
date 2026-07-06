
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity with external resources/links.
/// </summary>
public interface IWithResources
{
    /// <summary>
    ///   External resources/links associated with the entity.
    /// </summary>
    IReadOnlyList<Resource> Resources { get; }
}
