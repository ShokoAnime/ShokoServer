
#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member within a season.
/// </summary>
public class TMDB_Show_Crew : TMDB_Crew
{
    #region Properties

    /// <summary>
    /// TMDB Show ID for the show this job belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// Number of episodes within this season the crew member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of season within this show the crew member have worked on.
    /// </summary>
    public int SeasonCount { get; set; }

    #endregion

    #region Methods

    #endregion
}
