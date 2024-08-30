using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_MovieRepository : BaseCachedRepository<CrossRef_AniDB_TMDB_Movie, int>
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _anidbAnimeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _anidbEpisodeIDs;
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _tmdbMovieIDs;

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbEpisodeID(int episodeId)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId));

    public CrossRef_AniDB_TMDB_Movie? GetByAnidbEpisodeAndTmdbMovieIDs(int episodeId, int movieId)
        => GetByAnidbEpisodeID(episodeId).FirstOrDefault(xref => xref.TmdbMovieID == movieId);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByTmdbMovieID(int movieId)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId));

    public ILookup<int, CrossRef_AniDB_TMDB_Movie> GetByAnimeIDsAndType(IReadOnlyCollection<int> animeIds)
    {
        if (animeIds == null || animeIds?.Count == 0)
            return EmptyLookup<int, CrossRef_AniDB_TMDB_Movie>.Instance;

        return Lock(
            () => animeIds!.SelectMany(animeId => _anidbAnimeIDs!.GetMultiple(animeId)).ToLookup(xref => xref.AnidbAnimeID)
        );
    }

    protected override int SelectKey(CrossRef_AniDB_TMDB_Movie entity)
        => entity.CrossRef_AniDB_TMDB_MovieID;

    public override void PopulateIndexes()
    {
        _tmdbMovieIDs = new(Cache, a => a.TmdbMovieID);
        _anidbAnimeIDs = new(Cache, a => a.AnidbAnimeID);
        _anidbEpisodeIDs = new(Cache, a => a.AnidbEpisodeID);
    }

    public override void RegenerateDb()
    {
    }

    public CrossRef_AniDB_TMDB_MovieRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
