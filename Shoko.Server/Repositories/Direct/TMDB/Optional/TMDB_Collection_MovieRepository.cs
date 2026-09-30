using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Shoko.Abstractions.Metadata;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

namespace Shoko.Server.Repositories.Direct.TMDB.Optional;

public class TMDB_Collection_MovieRepository(DatabaseFactory databaseFactory) : BaseDirectRepository<TMDB_Collection_Movie, int>(databaseFactory)
{
    /// <inheritdoc />
    /// <remarks>
    ///   A series linked to a movie may be named by the movie's collection,
    ///   so the movie and the collection are forgotten when it joins or
    ///   leaves.
    /// </remarks>
    protected override IEnumerable<MetadataGuid> TextEntriesOf(TMDB_Collection_Movie entity, bool removed)
        =>
        [
            new(MetadataSource.TMDB, MetadataEntityType.Movie, entity.TmdbMovieID.ToString(CultureInfo.InvariantCulture)),
            new(MetadataSource.TMDB, MetadataEntityType.Collection, entity.TmdbCollectionID.ToString(CultureInfo.InvariantCulture)),
        ];

    public IReadOnlyList<TMDB_Collection_Movie> GetByTmdbCollectionID(int collectionId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Collection_Movie>()
            .Where(a => a.TmdbCollectionID == collectionId)
            .ToList();
    }

    public TMDB_Collection_Movie? GetByTmdbMovieID(int movieId)
    {
        using var session = _databaseFactory.SessionFactory.OpenSession();
        return session
            .Query<TMDB_Collection_Movie>()
            .Where(a => a.TmdbMovieID == movieId)
            .Take(1)
            .SingleOrDefault();
    }
}
