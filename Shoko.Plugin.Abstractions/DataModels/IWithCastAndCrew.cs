
using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels;

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
