using System.Collections.Generic;
using Shoko.Abstractions.Enums;

namespace Shoko.Abstractions.Metadata.Containers;

/// <summary>
/// Represents an entity with yearly seasons.
/// </summary>
public interface IWithYearlySeasons
{
    /// <summary>
    /// Get all yearly seasons the entity was released in.
    /// </summary>
    IReadOnlyList<(int Year, YearlySeason Season)> YearlySeasons { get; }
}
