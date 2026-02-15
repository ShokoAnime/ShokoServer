using System;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
///   Represents an entity with a last updated date.
/// </summary>
public interface IWithUpdateDate
{
    /// <summary>
    ///   When the entity was last updated in the local system, be it when
    ///   refreshing from a remote source, from the file system, or manually by
    ///   the user, depending on the entity in question.
    /// </summary>
    DateTime LastUpdatedAt { get; }
}
