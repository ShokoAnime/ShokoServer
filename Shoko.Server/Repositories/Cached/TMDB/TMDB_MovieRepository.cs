using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

#nullable enable
namespace Shoko.Server.Repositories.Cached.TMDB;

public class TMDB_MovieRepository : BaseCachedRepository<TMDB_Movie, int>
{
    protected override int SelectKey(TMDB_Movie entity) => entity.Id;
    private PocoIndex<int, TMDB_Movie, int> _movieIDs = null!;
    private PocoIndex<int, TMDB_Movie, int?> _collectionIDs = null!;

    public override void PopulateIndexes()
    {
        _movieIDs = Cache.CreateIndex(a => a.TmdbMovieID);
        _collectionIDs = Cache.CreateIndex(a => a.TmdbCollectionID);
    }

    public TMDB_Movie? GetByTmdbMovieID(int tmdbMovieId)
    {
        return _movieIDs.GetOne(tmdbMovieId);
    }

    public IReadOnlyList<TMDB_Movie> GetByTmdbCollectionID(int tmdbCollectionId)
    {
        return _collectionIDs.GetMultiple(tmdbCollectionId).OrderBy(a => a.EnglishTitle)
            .ThenBy(a => a.TmdbMovieID)
            .ToList();
    }

    public TMDB_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
