using System.Collections.Generic;

namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

/// <summary>
/// Shoko series metadata.
/// </summary>
public interface IShokoSeries : ISeries
{
    /// <summary>
    /// AniDB anime id linked to the Shoko series.
    /// </summary>
    int AnidbAnimeID { get; }

    /// <summary>
    /// The id of the direct parent group of the series
    /// </summary>
    int ParentGroupID { get; }

    /// <summary>
    /// The id of the top-level parent group of the series.
    /// </summary>
    int TopLevelGroupID { get; }

    /// <summary>
    /// A direct link to the anidb series metadata.
    /// </summary>
    ISeries AnidbAnime { get; }

    /// <summary>
    /// All series linked to this shoko series.
    /// </summary>
    IReadOnlyList<ISeries> LinkedSeries { get; }

    /// <summary>
    /// All movies linked to this shoko series.
    /// </summary>
    IReadOnlyList<IMovie> LinkedMovies { get; }

    /// <summary>
    /// The direct parent group of the series.
    /// </summary>
    IShokoGroup ParentGroup { get; }

    /// <summary>
    /// The top-level parent group of the series. It may or may not be the same
    /// as <see cref="ParentGroup"/> depending on how nested your group
    /// structure is.
    /// </summary>
    IShokoGroup TopLevelGroup { get; }

    /// <summary>
    /// Get an enumerable for all parent groups, starting at the
    /// <see cref="ParentGroup"/> all the way up to the <see cref="TopLevelGroup"/>.
    /// </summary>
    IReadOnlyList<IShokoGroup> AllParentGroups { get; }

    /// <summary>
    /// All episodes for the the shoko series.
    /// </summary>
    new IReadOnlyList<IShokoEpisode> Episodes { get; }
}
