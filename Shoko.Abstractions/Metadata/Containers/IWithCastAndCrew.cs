
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with cast and crew.
/// </summary>
public interface IWithCastAndCrew
{
    /// <summary>
    /// Cast associated with the entity.
    /// </summary>
    IReadOnlyList<ICast> Cast { get; }

    /// <summary>
    /// Crew associated with the entity.
    /// </summary>
    IReadOnlyList<ICrew> Crew { get; }
}
