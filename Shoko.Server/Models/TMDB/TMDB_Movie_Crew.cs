
#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for a movie.
/// </summary>
public class TMDB_Movie_Crew : TMDB_Crew
{
    #region Properties

    /// <summary>
    ///  Local ID.
    /// </summary>
    public int TMDB_Movie_CrewID { get; set; }

    /// <summary>
    /// TMDB Movie ID for the movie this job belongs to.
    /// </summary>
    public int TmdbMovieID { get; set; }

    #endregion

    #region Methods

    #endregion
}
