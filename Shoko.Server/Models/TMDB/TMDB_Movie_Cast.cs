
#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie_Cast : TMDB_Cast
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Movie_CastID { get; set; }

    /// <summary>
    /// TMDB Movie ID for the movie this role belongs to.
    /// </summary>
    public int TmdbMovieID { get; set; }

    #endregion

    #region Methods

    #endregion
}
