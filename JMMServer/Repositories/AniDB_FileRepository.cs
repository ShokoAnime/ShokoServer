using System.Collections.Generic;
using System.Linq;
using JMMServer.Entities;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class AniDB_FileRepository
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private static PocoCache<int, AniDB_File> Cache;
        private static PocoIndex<int, AniDB_File, string> Hashes;
        private static PocoIndex<int, AniDB_File, string> SHA1s;
        private static PocoIndex<int, AniDB_File, string> MD5s;
        private static PocoIndex<int, AniDB_File, int> FileIds;
        private static PocoIndex<int, AniDB_File, int> Animes;
        private static PocoIndex<int, AniDB_File, string> Resolutions;
        private static PocoIndex<int, AniDB_File, int> InternalVersions;
        public static void InitCache()
        {
            string t = "AniDB_File";
            ServerState.Instance.CurrentSetupStatus = string.Format(JMMServer.Properties.Resources.Database_Cache, t, string.Empty);
            AniDB_FileRepository repo = new AniDB_FileRepository();
            Cache = new PocoCache<int, AniDB_File>(repo.InternalGetAll(), a => a.AniDB_FileID);
            Hashes = new PocoIndex<int, AniDB_File, string>(Cache, a => a.Hash);
            SHA1s = new PocoIndex<int, AniDB_File, string>(Cache, a => a.SHA1);
            MD5s = new PocoIndex<int, AniDB_File, string>(Cache, a => a.MD5);
            FileIds = new PocoIndex<int, AniDB_File, int>(Cache, a => a.FileID);
            Animes = new PocoIndex<int, AniDB_File, int>(Cache, a => a.AnimeID);
            Resolutions = new PocoIndex<int, AniDB_File, string>(Cache,a=>a.File_VideoResolution);
            InternalVersions = new PocoIndex<int, AniDB_File, int>(Cache, a => a.InternalVersion);
        }
        internal List<AniDB_File> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }
        }

        public void Save(AniDB_File obj, bool updateStats)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    session.SaveOrUpdate(obj);
                    transaction.Commit();
                    Cache.Update(obj);
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
            return Cache.Get(id);
        }

        public AniDB_File GetByHash(string hash)
        {
            return Hashes.GetOne(hash);
        }

        public AniDB_File GetByHash(ISession session, string hash)
        {
            return Hashes.GetOne(hash);
/*            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("Hash", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public AniDB_File GetBySHA1(ISession session, string hash)
        {
            return SHA1s.GetOne(hash);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("SHA1", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public AniDB_File GetByMD5(ISession session, string hash)
        {
            return MD5s.GetOne(hash);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("MD5", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public List<AniDB_File> GetByInternalVersion(int version)
        {
            return InternalVersions.GetMultiple(version);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("MD5", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public AniDB_File GetByHashAndFileSize(string hash, long fsize)
        {
            return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
            /*using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_File cr = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("Hash", hash))
                    .Add(Restrictions.Eq("FileSize", fsize))
                    .UniqueResult<AniDB_File>();
                return cr;
            }*/
        }

        public AniDB_File GetByFileID(int fileID)
        {
            return FileIds.GetOne(fileID);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                AniDB_File cr = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("FileID", fileID))
                    .UniqueResult<AniDB_File>();
                return cr;
            }*/
        }

        public List<AniDB_File> GetAll()
        {
            return Cache.Values.ToList();
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }*/
        }

        public List<AniDB_File> GetByAnimeID(int animeID)
        {
            return Animes.GetMultiple(animeID);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("AnimeID", animeID))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }*/
        }

        public List<AniDB_File> GetByResolution(string res)
        {
            return Resolutions.GetMultiple(res);
            /*
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(AniDB_File))
                    .Add(Restrictions.Eq("File_VideoResolution", res))
                    .List<AniDB_File>();

                return new List<AniDB_File>(objs);
            }*/
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
                        Cache.Remove(cr);
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