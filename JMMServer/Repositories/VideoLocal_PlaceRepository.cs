using System;
using System.Collections.Generic;
using System.Linq;
using JMMServer.Databases;
using JMMServer.Entities;
using NutzCode.InMemoryIndex;

namespace JMMServer.Repositories
{
    public class VideoLocal_PlaceRepository
    {
        private static PocoCache<int, VideoLocal_Place> Cache;
        private static PocoIndex<int, VideoLocal_Place, int> VideoLocals;
        private static PocoIndex<int, VideoLocal_Place, int> ImportFolders;
        private static PocoIndex<int, VideoLocal_Place, string> Paths;
        
        public static void InitCache()
        {
            string t = "VideoLocal_Places";
            ServerState.Instance.CurrentSetupStatus = string.Format(DatabaseHelper.InitCacheTitle, t, string.Empty);
            VideoLocal_PlaceRepository repo = new VideoLocal_PlaceRepository();
            Cache = new PocoCache<int, VideoLocal_Place>(repo.InternalGetAll(), a => a.VideoLocal_Place_ID);
            VideoLocals = new PocoIndex<int, VideoLocal_Place, int>(Cache, a => a.VideoLocalID);
            ImportFolders = new PocoIndex<int, VideoLocal_Place, int>(Cache, a => a.ImportFolderID);
            Paths = new PocoIndex<int, VideoLocal_Place, string>(Cache,a=>a.FilePath);
        }

        public List<VideoLocal_Place> GetByImportFolder(int importFolderID)
        {
            return ImportFolders.GetMultiple(importFolderID);
        }
        public VideoLocal_Place GetByFilePathAndShareID(string filePath, int nshareID)
        {
            return Paths.GetMultiple(filePath).FirstOrDefault(a => a.ImportFolderID == nshareID);
        }

        public List<VideoLocal_Place> GetAll()
        {
            return Cache.Values.ToList();
        }
        public static Tuple<ImportFolder, string> GetFromFullPath(string fullPath)
        {
            List<ImportFolder> shares = new ImportFolderRepository().GetAll();

            // TODO make sure that import folders are not sub folders of each other
            // TODO make sure import folders do not contain a trailing "\"
            foreach (ImportFolder ifolder in shares)
            {
                string importLocation = ifolder.ImportFolderLocation;
                string importLocationFull = importLocation.TrimEnd('\\');

                // add back the trailing back slashes
                importLocationFull = importLocationFull + "\\";

                importLocation = importLocation.TrimEnd('\\');
                if (fullPath.StartsWith(importLocationFull))
                {
                    string filePath = fullPath.Replace(importLocation, string.Empty);
                    filePath = filePath.TrimStart('\\');
                    return new Tuple<ImportFolder, string>(ifolder,filePath);
                }
            }
            return null;
        }



        private List<VideoLocal_Place> InternalGetAll()
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                var objs = session
                    .CreateCriteria(typeof(VideoLocal_Place))
                    .List<VideoLocal_Place>();

                return new List<VideoLocal_Place>(objs);
            }
        }

        public void Save(VideoLocal_Place obj)
        {
            lock (obj)
            {
                using (var session = JMMService.SessionFactory.OpenSession())
                {
                    // populate the database
                    using (var transaction = session.BeginTransaction())
                    {
                        session.SaveOrUpdate(obj);
                        transaction.Commit();
                    }
                    Cache.Update(obj);
                }
            }
        }

        public VideoLocal_Place GetByID(int id)
        {
            return Cache.Get(id);
        }

        public List<VideoLocal_Place> GetByVideoLocal(int videolocalid)
        {
            return VideoLocals.GetMultiple(videolocalid);
        }


        public void Delete(int id)
        {
            using (var session = JMMService.SessionFactory.OpenSession())
            {
                // populate the database
                using (var transaction = session.BeginTransaction())
                {
                    VideoLocal_Place cr = GetByID(id);
                    if (cr != null)
                    {
                        Cache.Remove(cr);
                        session.Delete(cr);
                        transaction.Commit();
                    }
                }
            }
        }
    }
}
