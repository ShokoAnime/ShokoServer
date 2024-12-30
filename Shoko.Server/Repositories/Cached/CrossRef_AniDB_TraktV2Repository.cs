using System.Collections.Generic;
using System.Linq;
using NHibernate;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Databases;

#nullable enable
#pragma warning disable CA1822
namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_TraktV2Repository(DatabaseFactory databaseFactory) : BaseCachedRepository<CrossRef_AniDB_TraktV2, int>(databaseFactory)
{
    private PocoIndex<int, CrossRef_AniDB_TraktV2, int>? _animeIDs;

    protected override int SelectKey(CrossRef_AniDB_TraktV2 entity)
        => entity.CrossRef_AniDB_TraktV2ID;

    public override void PopulateIndexes()
    {
        _animeIDs = new PocoIndex<int, CrossRef_AniDB_TraktV2, int>(Cache, a => a.AnimeID);
    }

    public IReadOnlyList<CrossRef_AniDB_TraktV2> GetByAnimeID(int id)
        => _animeIDs!.GetMultiple(id).OrderBy(a => a.AniDBStartEpisodeType).ThenBy(a => a.AniDBStartEpisodeNumber).ToList();

    public IReadOnlyList<CrossRef_AniDB_TraktV2> GetByAnimeIDEpTypeEpNumber(int id, int aniEpType, int aniEpisodeNumber)
        => _animeIDs!.GetMultiple(id).Where(a => a.AniDBStartEpisodeType == aniEpType && a.AniDBStartEpisodeNumber == aniEpisodeNumber).ToList();

    public CrossRef_AniDB_TraktV2? GetByTraktID(ISession session, string id, int season, int episodeNumber, int animeID, int aniEpType, int aniEpisodeNumber)
        => Lock(() =>
        {
            var cr = session
                .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                .Add(Restrictions.Eq("TraktID", id))
                .Add(Restrictions.Eq("TraktSeasonNumber", season))
                .Add(Restrictions.Eq("TraktStartEpisodeNumber", episodeNumber))
                .Add(Restrictions.Eq("AnimeID", animeID))
                .Add(Restrictions.Eq("AniDBStartEpisodeType", aniEpType))
                .Add(Restrictions.Eq("AniDBStartEpisodeNumber", aniEpisodeNumber))
                .UniqueResult<CrossRef_AniDB_TraktV2>();
            return cr;
        });

    public CrossRef_AniDB_TraktV2? GetByTraktID(string id, int season, int episodeNumber, int animeID, int aniEpType, int aniEpisodeNumber)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            return GetByTraktID(session, id, season, episodeNumber, animeID, aniEpType, aniEpisodeNumber);
        });

    public IReadOnlyList<CrossRef_AniDB_TraktV2> GetByTraktID(string traktID)
        => Lock(() =>
        {
            using var session = _databaseFactory.SessionFactory.OpenSession();
            var xrefs = session
                .CreateCriteria(typeof(CrossRef_AniDB_TraktV2))
                .Add(Restrictions.Eq("TraktID", traktID))
                .List<CrossRef_AniDB_TraktV2>();

            return new List<CrossRef_AniDB_TraktV2>(xrefs);
        });

    internal ILookup<int, CrossRef_AniDB_TraktV2> GetByAnimeIDs(IReadOnlyCollection<int> animeIds)
        => ReadLock(() => animeIds.SelectMany(id => _animeIDs!.GetMultiple(id)).ToLookup(xref => xref.AnimeID));
}
