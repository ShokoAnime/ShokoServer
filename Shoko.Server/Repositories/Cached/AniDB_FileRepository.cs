using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Shoko.Models.Server;
using Shoko.Server.Repositories.NHibernate;
using NHibernate;
using NHibernate.Criterion;
using NLog;
using NutzCode.InMemoryIndex;
using Shoko.Server.Entities;

namespace Shoko.Server.Repositories
{
    public class AniDB_FileRepository : BaseCachedRepository<SVR_AniDB_File, int>
    {
        private static Logger logger = LogManager.GetCurrentClassLogger();

        private PocoIndex<int, SVR_AniDB_File, string> Hashes;
        private PocoIndex<int, SVR_AniDB_File, string> SHA1s;
        private PocoIndex<int, SVR_AniDB_File, string> MD5s;
        private PocoIndex<int, SVR_AniDB_File, int> FileIds;
        private PocoIndex<int, SVR_AniDB_File, int> Animes;
        private PocoIndex<int, SVR_AniDB_File, string> Resolutions;
        private PocoIndex<int, SVR_AniDB_File, int> InternalVersions;

        private AniDB_FileRepository()
        {
            EndDeleteCallback = (cr) =>
            {
                if (cr.AnimeID > 0)
                    SVR_AniDB_Anime.UpdateStatsByAnimeID(cr.AnimeID);
            };
        }

        public static AniDB_FileRepository Create()
        {
            return new AniDB_FileRepository();
        }

        protected override int SelectKey(SVR_AniDB_File entity)
        {
            return entity.AniDB_FileID;
        }

        public override void PopulateIndexes()
        {
            Hashes = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.Hash);
            SHA1s = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.SHA1);
            MD5s = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.MD5);
            FileIds = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.FileID);
            Animes = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.AnimeID);
            Resolutions = new PocoIndex<int, SVR_AniDB_File, string>(Cache, a => a.File_VideoResolution);
            InternalVersions = new PocoIndex<int, SVR_AniDB_File, int>(Cache, a => a.InternalVersion);
        }

        public override void RegenerateDb()
        {
        }

        //Disable base saves.
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(SVR_AniDB_File obj) { throw new NotSupportedException(); }
        [EditorBrowsable(EditorBrowsableState.Never)]
        [Obsolete("...", false)]
        public override void Save(IReadOnlyCollection<SVR_AniDB_File> objs) { throw new NotSupportedException(); }

        public void Save(SVR_AniDB_File obj, bool updateStats)
        {
            base.Save(obj);
            if (updateStats)
            {
                logger.Trace("Updating group stats by file from AniDB_FileRepository.Save: {0}", obj.Hash);
                SVR_AniDB_Anime.UpdateStatsByAnimeID(obj.AnimeID);
            }
        }


        public SVR_AniDB_File GetByHash(string hash)
        {
            return Hashes.GetOne(hash);
            /*            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("Hash", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }

        public SVR_AniDB_File GetBySHA1(string hash)
        {
            return SHA1s.GetOne(hash);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("SHA1", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public SVR_AniDB_File GetByMD5(string hash)
        {
            return MD5s.GetOne(hash);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("MD5", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public List<SVR_AniDB_File> GetByInternalVersion(int version)
        {
            return InternalVersions.GetMultiple(version);
            /*
            AniDB_File cr = session
                .CreateCriteria(typeof(AniDB_File))
                .Add(Restrictions.Eq("MD5", hash))
                .UniqueResult<AniDB_File>();
            return cr;*/
        }
        public SVR_AniDB_File GetByHashAndFileSize(string hash, long fsize)
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

        public SVR_AniDB_File GetByFileID(int fileID)
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



        public List<SVR_AniDB_File> GetByAnimeID(int animeID)
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

        public List<SVR_AniDB_File> GetByResolution(string res)
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

    }
}