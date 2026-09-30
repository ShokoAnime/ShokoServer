using Shoko.Abstractions.Metadata;
using Shoko.Server.Repositories;

namespace Shoko.Server.Models.TMDB;

public class TMDB_Movie_Cast : TMDB_Cast, ICast<IMovie>
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

    #region ICast Implementation

    IMovie? ICast<IMovie>.ParentOfType => GetTmdbMovie();

    #endregion
}
