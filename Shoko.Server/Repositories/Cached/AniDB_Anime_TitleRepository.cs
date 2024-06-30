using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Commons.Properties;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;
using Shoko.Server.Server;

namespace Shoko.Server.Repositories.Cached;

public class AniDB_Anime_TitleRepository : BaseCachedRepository<SVR_AniDB_Anime_Title, int>
{
    private PocoIndex<int, SVR_AniDB_Anime_Title, int> Animes;

    public override void PopulateIndexes()
    {
        Animes = new PocoIndex<int, SVR_AniDB_Anime_Title, int>(Cache, a => a.AnimeID);
    }

    protected override int SelectKey(SVR_AniDB_Anime_Title entity)
    {
        return entity.AniDB_Anime_TitleID;
    }

    public override void RegenerateDb()
    {
        // Don't need lock in init
        ServerState.Instance.ServerStartingStatus = string.Format(
            Resources.Database_Validating, typeof(SVR_AniDB_Anime_Title).Name, " DbRegen");
        var titles = Cache.Values.Where(title => title.Title.Contains('`')).ToList();
        foreach (var title in titles)
        {
            title.Title = title.Title.Replace('`', '\'');
            Save(title);
        }
    }

    public List<SVR_AniDB_Anime_Title> GetByAnimeID(int id)
    {
        return ReadLock(() => Animes.GetMultiple(id));
    }

    public ILookup<int, SVR_AniDB_Anime_Title> GetByAnimeIDs(ISessionWrapper session, ICollection<int> ids)
    {
        if (session == null)
        {
            throw new ArgumentNullException(nameof(session));
        }

        if (ids == null)
        {
            throw new ArgumentNullException(nameof(ids));
        }

        if (ids.Count == 0)
        {
            return EmptyLookup<int, SVR_AniDB_Anime_Title>.Instance;
        }

        return Lock(session, s =>
        {
            var titles = s.CreateCriteria<SVR_AniDB_Anime_Title>()
                .Add(Restrictions.InG(nameof(SVR_AniDB_Anime_Title.AnimeID), ids))
                .List<SVR_AniDB_Anime_Title>()
                .ToLookup(t => t.AnimeID);

            return titles;
        });
    }

    public AniDB_Anime_TitleRepository(DatabaseFactory databaseFactory) : base(databaseFactory)
    {
    }
}
