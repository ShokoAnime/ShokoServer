using System.Collections.Generic;
using JMMServer.Entities;
using JMMServer.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;

namespace JMMServer.Repositories
{
    public class AniDB_FileRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public void Save(AniDB_File obj, bool updateStats)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                }
            }

            if (updateStats)
            {
                logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {0}", obj.Hash);
                AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
            }
        }

        public AniDB_File GetByID(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return session.Get<AniDB_File>(id);
            }
        }

        public AniDB_File GetByHash(string hash)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                return GetByHash(session.Wrap(), hash);
            }
        }

        public AniDB_File GetByHash(ISessionWrapper session, string hash)
        {
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("Hash", hash))
                .UniqueResult<AniDB_File>();
            return cr;
        }

        public AniDB_File GetByHashAndFileSize(string hash, long fsize)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_File cr = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("FileSize", fsize))
                    .UniqueResult<AniDB_File>();
                return cr;
            }
        }

        public AniDB_File GetByFileID(int fileID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_File cr = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("FileID", fileID))
                    .UniqueResult<AniDB_File>();
                return cr;
            }
        }

        public List<AniDB_File> GetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }
        }

        public List<AniDB_File> GetByAnimeID(int animeID)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }
        }

        public List<AniDB_File> GetByResolution(string res)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("File_VideoResolution", res))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }
        }

        public void Delete(int id)
        {
            int animeID = 0;
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    AniDB_File cr = GetByID(id);
                    animeID = cr.AnimeID;
                    if (cr != null)
                    {
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }

            if (animeID > 0)
                AniDB_Anime.UpdateStatsByAnimeID(animeID);
        }
    }
}