using System.Collections.Generic;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with content ratings.
/// </summary>
public interface IWithContentRatings
{
    /// <summary>
    /// Content ratings associated with the entity.
    /// </summary>
    IReadOnlyList<IContentRating> ContentRatings { get; }
}
