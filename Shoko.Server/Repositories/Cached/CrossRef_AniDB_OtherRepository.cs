using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Enums;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories.Cached;

public class CrossRef_AniDB_OtherRepository : BaseCachedRepository<CrossRef_AniDB_Other, int>
{
    private PocoIndex<int, CrossRef_AniDB_Other, (int, CrossRefType)> _animeIDTypes;

    public CrossRef_AniDB_Other GetByAnimeIDAndType(int animeID, CrossRefType xrefType)
    {
        return ReadLock(() => _animeIDTypes.GetOne((animeID, xrefType)));
    }

    /// <summary>
    /// Gets other cross references by anime ID.
    /// </summary>
    /// <param name="session">The NHibernate session.</param>
    /// <param name="animeIds">An optional list of anime IDs whose cross references are to be retrieved.
    /// Can be <c>null</c> to get cross references for ALL anime.</param>
    /// <param name="xrefTypes">The types of cross references to find.</param>
    /// <returns>A <see cref="ILookup{TKey,TElement}"/> that maps anime ID to their associated other cross references.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="session"/> is <c>null</c>.</exception>
    public ILookup<int, CrossRef_AniDB_Other> GetByAnimeIDsAndType(ISessionWrapper session,
        IReadOnlyCollection<int> animeIds,
        params CrossRefType[] xrefTypes)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));

        if (xrefTypes == null || xrefTypes.Length == 0 || animeIds is { Count: 0 }) return EmptyLookup<int, CrossRef_AniDB_Other>.Instance;

        return Lock(() =>
        {
            var criteria = session.CreateCriteria<CrossRef_AniDB_Other>().Add(Restrictions.In(nameof(CrossRef_AniDB_Other.CrossRefType), xrefTypes));
            if (animeIds != null) criteria = criteria.Add(Restrictions.InG(nameof(CrossRef_AniDB_Other.AnimeID), animeIds));
            var crossRefs = criteria.List<CrossRef_AniDB_Other>().ToLookup(cr => cr.AnimeID);
            return crossRefs;
        });
    }

    public override void PopulateIndexes()
    {
        _animeIDTypes = new PocoIndex<int, CrossRef_AniDB_Other, (int, CrossRefType)>(Cache, a => (a.AnimeID, (CrossRefType)a.CrossRefType));
    }

    public override void RegenerateDb()
    {
        
    }

    protected override int SelectKey(CrossRef_AniDB_Other entity)
    {
        return entity.CrossRef_AniDB_OtherID;
    }

    public CrossRef_AniDB_OtherRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
