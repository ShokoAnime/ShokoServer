
#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_Collection_Movie
{
    #region Properties

    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Collection_MovieID { get; set; }

    /// <summary>
    /// TMDB Collection ID.
    /// </summary>
    public int TmdbCollectionID { get; set; }

    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    public int TmdbMovieID { get; set; }

    /// <summary>
    /// Ordering of movies within the collection.
    /// </summary>
    public int Ordering { get; set; }

    #endregion

    #region Constructors

    public TMDB_Collection_Movie() { }

    public TMDB_Collection_Movie(int collectionId, int movieId, int ordering)
    {
        TmdbCollectionID = collectionId;
        TmdbMovieID = movieId;
        Ordering = ordering;
    }

    #endregion
}
