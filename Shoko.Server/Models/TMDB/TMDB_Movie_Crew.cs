using Shoko.Abstractions.Metadata;
using Shoko.Server.Repositories;

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

    /// <inheritdoc/>
    public override MetadataEntityType ParentType => MetadataEntityType.Movie;

    #endregion

    #region Methods

    public TMDB_Movie? GetTmdbMovie() =>
        RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    public override IMetadata? GetTmdbParent() =>
        GetTmdbMovie();

    #endregion

    #region ICrew Implementation

    IMovie? ICrew<IMovie>.ParentOfType => GetTmdbMovie();

    #endregion
}
