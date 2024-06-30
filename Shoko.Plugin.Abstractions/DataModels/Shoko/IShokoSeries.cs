
namespace Shoko.Plugin.Abstractions.DataModels.Shoko;

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
    /// The direct parent group of the series.
    /// </summary>
    IShokoGroup ParentGroup { get; }

    /// <summary>
    /// The top-level parent group of the series. It may or may not be the same
    /// as <see cref="ParentGroup"/> depending on how nested your group
    /// structure is.
    /// </summary>
    IShokoGroup TopLevelGroup { get; }
}
