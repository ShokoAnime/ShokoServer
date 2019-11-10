using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using NHibernate.Criterion;
using NutzCode.InMemoryIndex;
using Shoko.Commons.Collections;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;

namespace Shoko.Server.Repositories
{
    public class AniDB_Episode_TitleRepository : BaseCachedRepository<AniDB_Episode_Title, int>
    {
        private PocoIndex<int, AniDB_Episode_Title, int> Episodes;

        public override void PopulateIndexes()
        {
            Episodes = new PocoIndex<int, AniDB_Episode_Title, int>(Cache, a => a.AniDB_EpisodeID);
        }

        private AniDB_Episode_TitleRepository()
        {
        }

        public static AniDB_Episode_TitleRepository Create()
        {
            var repo = new AniDB_Episode_TitleRepository();
            RepoFactory.CachedRepositories.Add(repo);
            return repo;
        }

        protected override int SelectKey(AniDB_Episode_Title entity)
        {
            return entity.AniDB_Episode_TitleID;
        }

        public override void RegenerateDb()
        {
        }



        public ILookup<int, AniDB_Episode_Title> GetByEpisodeIDs([NotNull] ISessionWrapper session, ICollection<int> ids)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));
            if (ids == null)
                throw new ArgumentNullException(nameof(ids));

            if (ids.Count == 0)
            {
                return EmptyLookup<int, AniDB_Episode_Title>.Instance;
            }

            lock (globalDBLock)
            {
                var titles = session.CreateCriteria<AniDB_Episode_Title>()
                    .Add(Restrictions.InG(nameof(AniDB_Episode_Title.AniDB_EpisodeID), ids))
                    .List<AniDB_Episode_Title>()
                    .ToLookup(t => t.AniDB_EpisodeID);

                return titles;
            }
        }

        public List<AniDB_Episode_Title> GetByEpisodeIDAndLanguage(int id, string language)
        {
            lock (Cache)
            {
                return Episodes.GetMultiple(id).Where(a =>
                    a.Language.Equals(language, StringComparison.InvariantCultureIgnoreCase)).ToList();
            }
        }

        public List<AniDB_Episode_Title> GetByEpisodeID(int ID)
        {
            lock (Cache)
            {
                return Episodes.GetMultiple(ID);
            }
        }
    }
}
