
#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Cast member for an episode.
/// </summary>
public class TMDB_Episode_Cast : TMDB_Cast
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Episode_CastID { get; set; }

    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this role belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID for the episode this role belongs to.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Indicates the role is not a recurring role within the season.
    /// </summary>
    public bool IsGuestRole { get; set; }

    #endregion

    #region Methods

    #endregion
}
