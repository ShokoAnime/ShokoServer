using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with tags, genres among them.
/// </summary>
public interface IWithTags
{
    /// <summary>
    /// The entity's tags, genres included, each with the weight and spoiler
    /// flag it has on this entity.
    /// </summary>
    IReadOnlyList<ITag> Tags { get => []; }
}
