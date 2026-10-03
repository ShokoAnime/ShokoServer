using System;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity with a last updated date.
/// </summary>
public interface IWithUpdateDate
{
    /// <summary>
    ///   When the entity's stored data last changed, be it from a refresh, the
    ///   file system or the user. A refresh that changes nothing leaves it as
    ///   it is.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
