
using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with studios.
/// </summary>
public interface IWithStudios
{
    /// <summary>
    /// Studios associated with the entity.
    /// </summary>
    IReadOnlyList<IStudio> Studios { get; }
}
