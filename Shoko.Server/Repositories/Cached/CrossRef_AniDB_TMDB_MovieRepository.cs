using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Server.Databases;
using Shoko.Server.Models.CrossReference;

#nullable enable
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TMDB_MovieRepository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_TMDB_Movie, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _anidbAnimeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _anidbEpisodeIDs;

    private PocoIndex<int, CrossRef_AniDB_TMDB_Movie, int>? _tmdbMovieIDs;

    protected override int SelectKey(CrossRef_AniDB_TMDB_Movie entity)
        => entity.CrossRef_AniDB_TMDB_MovieID;

    public override void PopulateIndexes()
    {
        _tmdbMovieIDs = Cache.CreateIndex(a => a.TmdbMovieID);
        _anidbAnimeIDs = Cache.CreateIndex(a => a.AnidbAnimeID);
        _anidbEpisodeIDs = Cache.CreateIndex(a => a.AnidbEpisodeID);
    }

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbAnimeID(int animeId)
        => ReadLock(() => _anidbAnimeIDs!.GetMultiple(animeId));

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByAnidbEpisodeID(int episodeId)
        => ReadLock(() => _anidbEpisodeIDs!.GetMultiple(episodeId));

    public CrossRef_AniDB_TMDB_Movie? GetByAnidbEpisodeAndTmdbMovieIDs(int episodeId, int movieId)
        => GetByAnidbEpisodeID(episodeId).FirstOrDefault(xref => xref.TmdbMovieID == movieId);

    public IReadOnlyList<CrossRef_AniDB_TMDB_Movie> GetByTmdbMovieID(int movieId)
        => ReadLock(() => _tmdbMovieIDs!.GetMultiple(movieId));
}
