using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.TMDB;

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

    public IReadOnlyList<string> GetAllKeywords()
    {
        var localMovieIds = RepoFactory.AnimeSeries.GetAll()
            .SelectMany(s => s.TmdbMovieCrossReferences)
            .Select(xref => xref.TmdbMovieID)
            .ToHashSet();
        return Cache.GetAll()
            .Where(m => localMovieIds.Contains(m.TmdbMovieID))
            .SelectMany(m => m.Keywords)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Except([""])
            .Order()
            .ToList();
    }

    public IReadOnlyList<string> GetAllGenres()
    {
        var localMovieIds = RepoFactory.AnimeSeries.GetAll()
            .SelectMany(s => s.TmdbMovieCrossReferences)
            .Select(xref => xref.TmdbMovieID)
            .ToHashSet();
        return Cache.GetAll()
            .Where(m => localMovieIds.Contains(m.TmdbMovieID))
            .SelectMany(m => m.Genres)
            .Distinct(StringComparer.InvariantCultureIgnoreCase)
            .Except([""])
            .Order()
            .ToList();
    }

    public TMDB_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
