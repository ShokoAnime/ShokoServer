using System.Collections.Generic;
using Shoko.Abstractions.Metadata.Containers;

namespace Shoko.Abstractions.Metadata.Shoko;

/// <summary>
/// Fake "season" for the Shoko series.
/// </summary>
public interface IShokoSeason : ISeason, IWithCreationDate, IWithUpdateDate
{
    /// <summary>
    /// Get the Shoko series info for the "season," if available.
    /// </summary>
    new IShokoSeries Series { get; }

    /// <summary>
    /// All episodes for the Shoko series for the fake "season."
    /// </summary>
    new IReadOnlyList<IShokoEpisode> Episodes { get; }
}
