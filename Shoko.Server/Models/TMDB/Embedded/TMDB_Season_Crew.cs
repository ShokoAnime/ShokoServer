using Shoko.Abstractions.Metadata;
using Shoko.Server.Repositories;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member within a season.
/// </summary>
public class TMDB_Season_Crew : TMDB_Crew
{
    #region Properties

    /// <summary>
    /// TMDB Show ID for the show this job belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this job belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <inheritdoc/>
    public override int TmdbParentID => TmdbSeasonID;

    /// <summary>
    /// Number of episodes within this season the crew member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    #endregion

    #region Methods

    public TMDB_Season? GetTmdbSeason() =>
        RepoFactory.TMDB_Season.GetByTmdbSeasonID(TmdbSeasonID);

    public override IMetadata<int>? GetTmdbParent() =>
        GetTmdbSeason();


    #endregion
}
