using System;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity with a creation date.
/// </summary>
public interface IWithCreationDate
{
    /// <summary>
    ///   When the entity was initially created in the local system.
    /// </summary>
    DateTime CreatedAt { get; }
}
