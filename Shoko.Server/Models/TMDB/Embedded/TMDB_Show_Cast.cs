
#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member within a show.
/// </summary>
public class TMDB_Show_Cast : TMDB_Cast
{
    #region Properties

    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// Number of episodes within this show the cast member have worked on.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of season within this show the cast member have worked on.
    /// </summary>
    public int SeasonCount { get; set; }

    #endregion

    #region Methods

    #endregion
}
