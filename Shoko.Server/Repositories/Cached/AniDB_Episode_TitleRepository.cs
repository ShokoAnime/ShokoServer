using System;
using System.Collections.Generic;
using System.Linq;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Server.Models;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AniDB_Episode_TitleRepository : BaseCachedRepository<SVR_AniDB_Episode_Title, int>
    {
        private PocoIndex<int, SVR_AniDB_Episode_Title, int> Episodes;

        public override void PopulateIndexes()
        {
            Episodes = new PocoIndex<int, SVR_AniDB_Episode_Title, int>(Cache, a => a.AniDB_EpisodeID);
        }

        protected override int SelectKey(SVR_AniDB_Episode_Title entity)
        {
            return entity.AniDB_Episode_TitleID;
        }

        public override void RegenerateDb()
        {
        }

        public ILookup<int, SVR_AniDB_Episode_Title> GetByEpisodeIDs(ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, SVR_AniDB_Episode_Title>.Instance;
            }

            lock (globalDBLock)
            {
                var titles = session.CreateCriteria<SVR_AniDB_Episode_Title>()
                    .Add(Restrictions.InG(nameof(SVR_AniDB_Episode_Title.AniDB_EpisodeID), ids))
                    .List<SVR_AniDB_Episode_Title>()
                    .ToLookup(t => t.AniDB_EpisodeID);

                return titles;
            }
        }

        public List<SVR_AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int id, TitleLanguage language)
        {
            lock (Cache)
            {
                return Episodes.GetMultiple(id).Where(a => a.Language == language).ToList();
            }
        }

        public List<SVR_AniDB_Episode_Title> GetByEpisodeID(int ID)
        {
            lock (Cache)
            {
                return Episodes.GetMultiple(ID);
            }
        }
    }
}
