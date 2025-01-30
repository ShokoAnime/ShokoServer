using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

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

    public TMDB_Movie? GetTmdbMovie() =>
        RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    public override IMetadata<int>? GetTmdbParent() =>
        GetTmdbMovie();

    #endregion

    #region ICrew Implementation

    IMovie? ICrew<IMovie>.ParentOfType => GetTmdbMovie();

    #endregion
}
