using System;
using System.Collections.Generic;
using System.Linq;
using NutzCode.InMemoryIndex;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories.ReaderWriterLockExtensions;

namespace Shoko.Server.Repositories.Repos
{
    public class VideoLocalRepository : BaseRepository<SVR_VideoLocal, int, bool>
    {
        //private readonly Logger logger = LogManager.GetCurrentClassLogger();
        private PocoIndex<int, SVR_VideoLocal, string> Hashes;
        private PocoIndex<int, SVR_VideoLocal, string> SHA1;
        private PocoIndex<int, SVR_VideoLocal, string> MD5;
        private PocoIndex<int, SVR_VideoLocal, int> Ignored;


        internal override int SelectKey(SVR_VideoLocal entity) => entity.VideoLocalID;

        internal override object BeginDelete(SVR_VideoLocal entity, bool updateEpisodes)
        {
            List<int> eps= entity.GetAnimeEpisodes().Select(a=>a.AnimeEpisodeID).ToList();
            Repo.Instance.VideoLocal_Place.Delete(entity.Places);
            Repo.Instance.VideoLocal_User.Delete(Repo.Instance.VideoLocal_User.GetByVideoLocalID(entity.VideoLocalID));
            return eps;

        }

        public List<SVR_VideoLocal> GetAllLimit(int limit)
        {
            using (RepoLock.ReaderLock())
            {
                return IsCached ? Cache.Values.Take(limit).ToList() : Table.Take(limit).ToList();
            }
        }
        internal override void EndDelete(SVR_VideoLocal entity, object returnFromBeginDelete, bool updateEpisodes)
        {
            Repo.Instance.AnimeEpisode.Touch(()=>Repo.Instance.AnimeEpisode.GetMany((List<int>)returnFromBeginDelete));
        }


        internal override void PopulateIndexes()
        {
            Hashes = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.Hash);
            SHA1 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.SHA1);
            MD5 = new PocoIndex<int, SVR_VideoLocal, string>(Cache, a => a.MD5);
            Ignored = new PocoIndex<int, SVR_VideoLocal, int>(Cache, a => a.IsIgnored);
        }

        internal override void ClearIndexes()
        {
            Hashes = null;
            SHA1 = null;
            MD5 = null;
            Ignored = null;
        }
        public override void PreInit(IProgress<InitProgress> progress, int batchSize)
        {
            List<SVR_VideoLocal> emptylist = Where(a => a.IsEmpty()).ToList();
            InitProgress regen = new InitProgress();
            if (emptylist.Count > 0)
            {
                regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, typeof(VideoLocal).Name, " Cleaning Empty Records");
                regen.Step = 0;
                regen.Total = emptylist.Count;
                progress.Report(regen);
                foreach (SVR_VideoLocal vl in emptylist)
                {
                    Delete(vl);
                    regen.Step++;
                    progress.Report(regen);
                }
            }
            Dictionary<string, List<SVR_VideoLocal>> locals = Where(a => !string.IsNullOrWhiteSpace(a.Hash)).GroupBy(a => a.Hash).Where(a=>a.Count()>1).ToDictionary(g => g.Key, g => g.ToList());
            if (locals.Count > 0)
            {
                var toRemove = new List<SVR_VideoLocal>();
                var comparer = new VideoLocalComparer();
                regen.Step = 0;
                regen.Total = locals.Count;
                regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, typeof(VideoLocal).Name, " Cleaning Duplicate Records");
                foreach (string hash in locals.Keys)
                {
                    List<SVR_VideoLocal> values = locals[hash];
                    values.Sort(comparer);
                    SVR_VideoLocal to = values.First();
                    List<SVR_VideoLocal> froms = values.ToList();
                    froms.Remove(to);
                    foreach (SVR_VideoLocal from in froms)
                    {
                        using (var upd = Repo.Instance.VideoLocal_Place.BeginBatchUpdate(() => Repo.Instance.VideoLocal_Place.GetByVideoLocal(from.VideoLocalID)))
                        {
                            foreach (SVR_VideoLocal_Place place in upd)
                            {
                                place.VideoLocalID = to.VideoLocalID;
                                upd.Update(place);
                            }

                            upd.Commit();
                        }
                    }

                    regen.Step++;
                    progress.Report(regen);
                    toRemove.AddRange(froms);
                }
                Delete(toRemove);
            }

            List<CrossRef_File_Episode> frags = WhereAll().SelectMany(a => Repo.Instance.CrossRef_File_Episode.GetByHash(a.Hash)).AsQueryable().Where(a => Repo.Instance.AniDB_Anime.GetByID(a.AnimeID) == null || a.GetEpisode() == null).ToList();
            if (frags.Count > 0)
            {
                regen.Step = 0;
                regen.Total = frags.Count;
                regen.Title = string.Format(Commons.Properties.Resources.Database_Validating, typeof(VideoLocal).Name, " Cleaning Fragmented Records");
                Repo.Instance.CrossRef_File_Episode.Delete(frags);
            }
        }

        public IEnumerable<SVR_VideoLocal> GetVideosWithoutEpisodeUnsorted()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values
                        .Where(a => a.IsIgnored == 0 && !Repo.Instance.CrossRef_File_Episode.GetByHash(a.Hash).Any());
                else
                    return Table.Where(a => a.IsIgnored == 0 && !Repo.Instance.CrossRef_File_Episode.GetByHash(a.Hash).Any());
            }
        }

        public List<SVR_VideoLocal> GetByImportFolder(int importFolderID)
        {
            return Repo.Instance.VideoLocal_Place.GetByImportFolder(importFolderID).Select(a => a.VideoLocal)
                .Where(a => a != null)
                .Distinct()
                .ToList();
        }

        private void UpdateMediaContracts_RA(SVR_VideoLocal obj)
        {
            if (obj.Media == null || obj.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || obj.Duration == 0)
            {
                SVR_VideoLocal_Place place = obj.GetBestVideoLocalPlace();
                place?.RefreshMediaInfo(obj);
            }
        }

        internal override object BeginSave(SVR_VideoLocal entity, SVR_VideoLocal original_entity, bool updateEpisodes)
        {
            if (original_entity == null)
                entity.Media = null;
            UpdateMediaContracts_RA(entity);
            return null;
        }

        internal override void EndSave(SVR_VideoLocal entity, object returnFromBeginSave, bool updateEpisodes)
        {
            if (updateEpisodes)
                Repo.Instance.AnimeEpisode.Touch(()=>entity.GetAnimeEpisodes());
        }
        public SVR_VideoLocal GetByHash(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetOne(hash);
                return Table.FirstOrDefault(a => a.Hash == hash);
            }
        }
        public List<SVR_VideoLocal> GetByHashAndNotId(string hash, int id)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).Where(a=>a.VideoLocalID!=id).ToList();
                return Table.Where(a => a.Hash == hash && a.VideoLocalID!=id).ToList();
            }
        }

        internal IDictionary<int, DateTime> GetEpisodesRecentlyAdded()
        {
            using (RepoLock.ReaderLock())
            //using (Repo.Instace.CrossRef_File_Episode.RepoLock.ReaderLock()) //TODO: Test, this will probably lock.
            //using (Repo.Instace.AnimeEpisode.RepoLock.ReaderLock())
            {
                return Table
                        .Join(Repo.Instance.CrossRef_File_Episode.Table, vl => vl.Hash, xref => xref.Hash, (vl, xref) => new { vl, xref })
                        .Join(Repo.Instance.AnimeEpisode.Table, x => x.xref.EpisodeID, ae => ae.AniDB_EpisodeID, (jn, ae) => new { jn.vl, jn.xref, ae })
                        .GroupBy(jn => jn.ae.AnimeSeriesID, (_, group) => group.OrderByDescending(x => x.vl.DateTimeCreated).First())
                        .OrderByDescending(s => s.vl.DateTimeCreated)
                        .Select(grp => new { grp.ae.AnimeSeriesID, grp.vl.DateTimeCreated }).ToList()

                        .OrderByDescending(s => s.DateTimeCreated)
                        .ToDictionary(a => a.AnimeSeriesID, b => b.DateTimeCreated);
            }
        }

        public SVR_VideoLocal GetByMD5(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return MD5.GetOne(hash);
                return Table.FirstOrDefault(a => a.MD5==hash);
            }
        }

        public SVR_VideoLocal GetBySHA1(string hash)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return SHA1.GetOne(hash);
                return Table.FirstOrDefault(a => a.SHA1 == hash);
            }
        }

        public long GetTotalRecordCount()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Keys.Count;
                return Table.Count();
            }
        }

        public SVR_VideoLocal GetByHashAndSize(string hash, long fsize)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple(hash).FirstOrDefault(a => a.FileSize == fsize);
                return Table.FirstOrDefault(a => a.Hash == hash && a.FileSize==fsize);
            }
        }

        public List<SVR_VideoLocal> GetByName(string fileName)
        {
            return Where(p => p.Places.Any(
                    a => a.FilePath.Contains(fileName, StringComparison.InvariantCultureIgnoreCase)))
                .ToList();
        }

        public List<SVR_VideoLocal> GetMostRecentlyAdded(int maxResults)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                {
                    if (maxResults == -1)
                        return Cache.Values.OrderByDescending(a => a.DateTimeCreated).ToList();
                    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
                }
                if (maxResults == -1)
                    return Table.OrderByDescending(a => a.DateTimeCreated).ToList();
                return Table.OrderByDescending(a => a.DateTimeCreated).Take(maxResults).ToList();
            }
        }
        public List<string> GetHashesMostRecentlyAdded(int maxResults)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                {
                    if (maxResults == -1)
                        return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Select(a=>a.Hash).ToList();
                    return Cache.Values.OrderByDescending(a => a.DateTimeCreated).Select(a => a.Hash).Take(maxResults).ToList();
                }
                if (maxResults == -1)
                    return Table.OrderByDescending(a => a.DateTimeCreated).Select(a => a.Hash).ToList();
                return Table.OrderByDescending(a => a.DateTimeCreated).Select(a => a.Hash).Take(maxResults).ToList();
            }
        }
        public List<SVR_VideoLocal> GetRandomFiles(int maxResults)
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Cache.Values.OrderBy(a => Guid.NewGuid()).Take(maxResults).ToList();
                return Table.OrderBy(a => Guid.NewGuid()).Take(maxResults).ToList();
            }
        }

        /// <summary>
        /// returns all the VideoLocal records associate with an AnimeEpisode Record
        /// </summary>
        /// <param name="episodeID"></param>
        /// <returns></returns>
        /// 
        public List<SVR_VideoLocal> GetByAniDBEpisodeID(int episodeID)
        {
            return Repo.Instance.CrossRef_File_Episode.GetByEpisodeID(episodeID).Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }


        public List<SVR_VideoLocal> GetMostRecentlyAddedForAnime(int maxResults, int animeID)
        {
            return
                Repo.Instance.CrossRef_File_Episode.GetByAnimeID(animeID).Select(a => GetByHash(a.Hash))
                    .Where(a => a != null)
                    .OrderByDescending(a => a.DateTimeCreated)
                    .Take(maxResults)
                    .ToList();
        }


        public List<SVR_VideoLocal> GetByAniDBResolution(string res)
        {
            return Repo.Instance.AniDB_File.GetByResolution(res).Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }

        public List<SVR_VideoLocal> GetByInternalVersion(int iver)
        {
            return Repo.Instance.AniDB_File.GetByInternalVersion(iver).Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }

        public List<SVR_VideoLocal> GetWithMissingChapters()
        {
            return Repo.Instance.AniDB_File.GetWithWithMissingChapters().Select(a => GetByHash(a.Hash))
                .Where(a => a != null)
                .ToList();
        }

        /// <summary>
        /// returns all the VideoLocal records associate with an AniDB_Anime Record
        /// </summary>
        /// <param name="animeID"></param>
        /// <returns></returns>
        public List<SVR_VideoLocal> GetByAniDBAnimeID(int animeID)
        {
            return
                Repo.Instance.CrossRef_File_Episode.GetByAnimeID(animeID).Select(a => GetByHash(a.Hash))
                    .Where(a => a != null)
                    .ToList();
        }

        public List<SVR_VideoLocal> GetVideosWithoutHash()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Hashes.GetMultiple("").ToList();
                return Table.Where(a => a.Hash == "").ToList();
            }
        }

        public List<SVR_VideoLocal> GetVideosWithoutVideoInfo()
        {
            return Where(a => a.Media == null || a.MediaVersion < SVR_VideoLocal.MEDIA_VERSION || a.Duration == 0).ToList();
        }

        public List<SVR_VideoLocal> GetVideosWithoutEpisode()
        {
            HashSet<string> hashes = new HashSet<string>(Repo.Instance.CrossRef_File_Episode.GetAll().Select(a => a.Hash));
            HashSet<string> vlocals;
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                {
                    vlocals = new HashSet<string>(Cache.Values.Where(a => a.IsIgnored == 0).Select(a => a.Hash));
                    return vlocals.Except(hashes).SelectMany(a => Hashes.GetMultiple(a)).OrderBy(a => a.DateTimeCreated).ToList();
                }
                vlocals = new HashSet<string>(Table.Where(a => a.IsIgnored == 0).Select(a => a.Hash));
                return vlocals.Except(hashes).SelectMany(a => Table.Where(b=>b.Hash==a)).OrderBy(a => a.DateTimeCreated).ToList();
            }
        }

        public List<SVR_VideoLocal> GetManuallyLinkedVideos()
        {
            return
                Repo.Instance.CrossRef_File_Episode.GetAll().Where(a => a.CrossRefSource != 1)
                    .Select(a => GetByHash(a.Hash))
                    .Where(a => a != null)
                    .ToList();
        }

        public List<SVR_VideoLocal> GetIgnoredVideos()
        {
            using (RepoLock.ReaderLock())
            {
                if (IsCached)
                    return Ignored.GetMultiple(1);
                return Table.Where(a => a.IsIgnored==1).ToList();
            }
        }
        public List<string> GetVariationsHashes(bool variation)
        {
            using (RepoLock.ReaderLock())
            {
                if (variation)
                    return Where(a => !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
                return Where(a => a.IsVariation == 0 && !string.IsNullOrEmpty(a.Hash)).Select(a => a.Hash).ToList();
            }
        }

    }
}