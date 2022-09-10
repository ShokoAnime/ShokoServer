using System.Collections.Generic;
using NHibernate.Criterion;
using Shoko.Models.Server;
using Shoko.Server.Databases;

namespace Shoko.Server.Repositories.Direct
{
    public class CrossRef_AniDB_Trakt_EpisodeRepository : BaseDirectRepository<CrossRef_AniDB_Trakt_Episode, int>
    {

        public List<CrossRef_AniDB_Trakt_Episode> GetByAnimeID(int id)
        {
            lock (GlobalDBLock)
            {
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                var objs = session
                    .CreateCriteria(typeof(CrossRef_AniDB_Trakt_Episode))
                    .Add(Restrictions.Eq("AnimeID", id))
                    .List<CrossRef_AniDB_Trakt_Episode>();

                return new List<CrossRef_AniDB_Trakt_Episode>(objs);
            }
        }
    }
}