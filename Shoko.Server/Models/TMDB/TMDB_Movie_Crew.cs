using Shoko.Plugin.Abstractions.DataModels;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Crew member for a movie.
/// </summary>
public class TMDB_Movie_Crew : TMDB_Crew, ICrew<IMovie>
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

    /// <inheritdoc />
    public override int TmdbParentID => TmdbMovieID;

    #endregion

    #region Methods

    public virtual TMDB_Movie? Movie { get; set; }

    public override IMetadata<int>? GetTmdbParent() =>
        Movie;

    #endregion

    #region ICrew Implementation

    IMovie? ICrew<IMovie>.ParentOfType => Movie;

    #endregion
}
