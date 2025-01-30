using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Repositories;

#nullable enable
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

    #endregion

    #region Methods

    public TMDB_Movie? GetTmdbMovie() =>
        RepoFactory.TMDB_Movie.GetByTmdbMovieID(TmdbMovieID);

    public override IMetadata<int>? GetTmdbParent() =>
        GetTmdbMovie();

    #endregion

    #region ICast Implementation

    IMovie? ICast<IMovie>.ParentOfType => GetTmdbMovie();

    #endregion
}
