using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Models.WebCache;
using Shoko.Server.CommandQueue.Commands.AniDB;
using Shoko.Server.Import;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Raws;
using Shoko.Server.Providers.WebCache;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;

namespace Shoko.Server.CommandQueue.Commands.Server
{
    public class CmdServerProcessFile : BaseCommand, ICommand
    {
        private readonly SVR_VideoLocal vidLocal;

        public CmdServerProcessFile(string str) : base(str)
        {
        }

        public CmdServerProcessFile(int vidLocalID, bool forceAniDB)
        {
            VideoLocalID = vidLocalID;
            ForceAniDB = forceAniDB;
            vidLocal = Repo.Instance.VideoLocal.GetByID(VideoLocalID);
        }

        public CmdServerProcessFile(SVR_VideoLocal vidlocal, bool forceAniDB)
        {
            VideoLocalID = vidlocal?.VideoLocalID ?? 0;
            ForceAniDB = forceAniDB;
            vidLocal = vidlocal;
        }

        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }


        public string ParallelTag { get; set; } = WorkTypes.Server;
        public int ParallelMax { get; set; } = 8;
        public int Priority { get; set; } = 3;

        public string Id => $"ProcessFile_{VideoLocalID}";

        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.FileInfo, ExtraParams = new[] {vidLocal != null ? vidLocal.Info : VideoLocalID.ToString()}};

        public string WorkType => WorkTypes.Server;

        public override void Run(IProgress<ICommand> progress = null)
        {
            try
            {
                ReportInit(progress);
                if (vidLocal == null) 
                {
                    ReportFinish(progress);
                    return;
                }

                //now that we have all the has info, we can get the AniDB Info
                logger.Trace($"Checking for AniDB_File record for: {vidLocal.Hash} --- {vidLocal.Info}");
                // check if we already have this AniDB_File info in the database

                lock (vidLocal)
                {
                    List<ICommand> cmds = new List<ICommand>();
                    SVR_AniDB_File aniFile = null;

                    if (!ForceAniDB)
                    {
                        aniFile = Repo.Instance.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, vidLocal.FileSize);

                        if (aniFile == null)
                            logger.Trace("AniDB_File record not found");
                    }

                    // If cross refs were wiped, but the AniDB_File was not, we unfortunately need to requery the info
                    List<CrossRef_File_Episode> crossRefs = Repo.Instance.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
                    if (crossRefs == null || crossRefs.Count == 0) aniFile = null;
                    ReportUpdate(progress, 10);
                    int animeID = 0;

                    if (aniFile == null || ForceAniDB)
                    {
                        // get info from AniDB
                        logger.Debug("Getting AniDB_File record from AniDB....");

                        // check if we already have a record

                        using (var upd = Repo.Instance.AniDB_File.BeginAddOrUpdate(() => Repo.Instance.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, vidLocal.FileSize)))
                        {
                            bool skip = false;
                            if (!upd.IsUpdate || ForceAniDB)
                            {
                                Raw_AniDB_File fileInfo = ShokoService.AnidbProcessor.GetFileInfo(vidLocal);
                                if (fileInfo != null)
                                    upd.Entity.Populate_RA(fileInfo);
                                else skip = true;
                            }

                            if (!skip)
                            {
                                //overwrite with local file name
                                string localFileName = vidLocal.GetBestVideoLocalPlace()?.FullServerPath;
                                localFileName = !string.IsNullOrEmpty(localFileName) ? Path.GetFileName(localFileName) : vidLocal.Info;
                                upd.Entity.FileName = localFileName;

                                aniFile = upd.Commit();
                                aniFile.CreateLanguages();
                                aniFile.CreateCrossEpisodes(localFileName);

                                animeID = aniFile.AnimeID;
                            }
                        }

                        ReportUpdate(progress, 20);
                    }

                    bool missingEpisodes = false;

                    // if we still haven't got the AniDB_File Info we try the web cache or local records
                    if (aniFile == null)
                    {
                        // check if we have any records from previous imports
                        crossRefs = Repo.Instance.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
                        if (crossRefs == null || crossRefs.Count == 0)
                        {
                            // lets see if we can find the episode/anime info from the web cache
                            if (ServerSettings.Instance.WebCache.XRefFileEpisode_Get)
                            {
                                List<CrossRef_File_Episode> xrefs = WebCacheAPI.Instance.GetCrossRef_File_Episodes(vidLocal.Hash);


                                crossRefs = new List<CrossRef_File_Episode>();
                                if (xrefs == null || xrefs.Count == 0)
                                {
                                    logger.Debug($"Cannot find AniDB_File record or get cross ref from web cache record so exiting: {vidLocal.ED2KHash}");
                                    ReportFinish(progress);
                                    return;
                                }

                                string fileName = vidLocal.GetBestVideoLocalPlace()?.FullServerPath;
                                fileName = !string.IsNullOrEmpty(fileName) ? Path.GetFileName(fileName) : vidLocal.Info;
                                foreach (CrossRef_File_Episode xref in xrefs)
                                {
                                    CrossRef_File_Episode xrefEnt = new CrossRef_File_Episode
                                    {
                                        Hash = vidLocal.ED2KHash,
                                        FileName = fileName,
                                        FileSize = vidLocal.FileSize,
                                        CrossRefSource = (int) CrossRefSource.WebCache,
                                        AnimeID = xref.AnimeID,
                                        EpisodeID = xref.EpisodeID,
                                        Percentage = xref.Percentage,
                                        EpisodeOrder = xref.EpisodeOrder
                                    };
                                    bool duplicate = false;

                                    foreach (CrossRef_File_Episode xrefcheck in crossRefs)
                                    {
                                        if (xrefcheck.AnimeID == xrefEnt.AnimeID && xrefcheck.EpisodeID == xrefEnt.EpisodeID && xrefcheck.Hash == xrefEnt.Hash)
                                            duplicate = true;
                                    }

                                    if (!duplicate)
                                    {
                                        crossRefs.Add(xrefEnt);
                                        // in this case we need to save the cross refs manually as AniDB did not provide them
                                        Repo.Instance.CrossRef_File_Episode.BeginAdd(xrefEnt).Commit();
                                    }
                                }
                            }
                            else
                            {
                                logger.Debug($"Cannot get AniDB_File record so exiting: {vidLocal.ED2KHash}");
                                ReportFinish(progress);
                                return;
                            }
                        }

                        ReportUpdate(progress, 30);
                        // we assume that all episodes belong to the same anime
                        foreach (CrossRef_File_Episode xref in crossRefs)
                        {
                            animeID = xref.AnimeID;

                            AniDB_Episode ep = Repo.Instance.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                            if (ep == null) missingEpisodes = true;
                        }
                    }
                    else
                    {
                        // check if we have the episode info
                        // if we don't, we will need to re-download the anime info (which also has episode info)

                        if (aniFile.EpisodeCrossRefs.Count == 0)
                        {
                            animeID = aniFile.AnimeID;

                            // if we have the anidb file, but no cross refs it means something has been broken
                            logger.Debug($"Could not find any cross ref records for: {vidLocal.ED2KHash}");
                            missingEpisodes = true;
                        }
                        else
                        {
                            foreach (CrossRef_File_Episode xref in aniFile.EpisodeCrossRefs)
                            {
                                AniDB_Episode ep = Repo.Instance.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                                if (ep == null)
                                    missingEpisodes = true;

                                animeID = xref.AnimeID;
                            }
                        }

                        ReportUpdate(progress, 30);
                    }

                    // get from DB
                    SVR_AniDB_Anime anime = Repo.Instance.AniDB_Anime.GetByAnimeID(animeID);
                    var update = Repo.Instance.AniDB_AnimeUpdate.GetByAnimeID(animeID);
                    bool animeRecentlyUpdated = false;

                    if (anime != null && update != null)
                    {
                        TimeSpan ts = DateTime.Now - update.UpdatedAt;
                        if (ts.TotalHours < 4) animeRecentlyUpdated = true;
                    }
                    else
                        missingEpisodes = true;

                    // even if we are missing episode info, don't get data  more than once every 4 hours
                    // this is to prevent banning
                    if (missingEpisodes && !animeRecentlyUpdated)
                    {
                        logger.Debug("Getting Anime record from AniDB....");
                        anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true, ServerSettings.Instance.AutoGroupSeries || ServerSettings.Instance.AniDb.DownloadRelatedAnime);
                    }

                    ReportUpdate(progress, 40);
                    // create the group/series/episode records if needed
                    if (anime != null)
                    {
                        logger.Debug("Creating groups, series and episodes....");
                        // check if there is an AnimeSeries Record associated with this AnimeID
                        SVR_AnimeSeries ser;
                        using (var upd = Repo.Instance.AnimeSeries.BeginAddOrUpdate(() => Repo.Instance.AnimeSeries.GetByAnimeID(animeID), () => anime.CreateAnimeSeriesAndGroup()))
                        {
                            upd.Entity.CreateAnimeEpisodes();

                            // check if we have any group status data for this associated anime
                            // if not we will download it now
                            if (Repo.Instance.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                            {
                                cmds.Add(new CmdAniDBGetReleaseGroupStatus(anime.AnimeID, false));
                            }

                            // update stats
                            upd.Entity.EpisodeAddedDate = DateTime.Now;
                            ser = upd.Commit();
                        }

                        Repo.Instance.AnimeGroup.BatchAction(ser.AllGroupsAbove, ser.AllGroupsAbove.Count, (grp, _) => grp.EpisodeAddedDate = DateTime.Now, (true, false, false));
                        ReportUpdate(progress, 50);
                        // We do this inside, as the info will not be available as needed otherwise
                        List<SVR_VideoLocal> videoLocals = aniFile?.EpisodeIDs?.SelectMany(a => Repo.Instance.VideoLocal.GetByAniDBEpisodeID(a)).Where(b => b != null).ToList();
                        if (videoLocals != null)
                        {
                            // Copy over watched states
                            foreach (var user in Repo.Instance.JMMUser.GetAll())
                            {
                                var watchedVideo = videoLocals.FirstOrDefault(a => a?.GetUserRecord(user.JMMUserID)?.WatchedDate != null);
                                // No files that are watched
                                if (watchedVideo == null) continue;

                                var watchedRecord = watchedVideo.GetUserRecord(user.JMMUserID);

                                using (var upd = Repo.Instance.VideoLocal_User.BeginAddOrUpdate(() => vidLocal.GetUserRecord(user.JMMUserID), () => new VideoLocal_User {JMMUserID = user.JMMUserID, VideoLocalID = vidLocal.VideoLocalID}))
                                {
                                    upd.Entity.WatchedDate = watchedRecord.WatchedDate;
                                    upd.Entity.ResumePosition = watchedRecord.ResumePosition;
                                    upd.Commit();
                                }
                            }

                            ReportUpdate(progress, 60);
                            if (ServerSettings.Instance.FileQualityFilterEnabled)
                            {
                                videoLocals.Sort(FileQualityFilter.CompareTo);
                                List<SVR_VideoLocal> keep = videoLocals.Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep).ToList();
                                foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                                if (!FileQualityFilter.Settings.AllowDeletionOfImportedFiles && videoLocals.Contains(vidLocal)) videoLocals.Remove(vidLocal);
                                videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                                videoLocals.ForEach(a => a.Places.ForEach(b => b.RemoveAndDeleteFile()));
                            }
                        }

                        ReportUpdate(progress, 70);
                        // update stats for groups and series
                        // update all the groups above this series in the heirarchy
                        SVR_AniDB_Anime.UpdateStatsByAnimeID(animeID);
                    }
                    else
                    {
                        logger.Warn($"Unable to create AniDB_Anime for file: {vidLocal.Info}");
                    }

                    ReportUpdate(progress, 80);
                    vidLocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });
                    // Add this file to the users list
                    if (ServerSettings.Instance.AniDb.MyList_AddFiles)
                    {
                        cmds.Add(new CmdAniDBAddFileToMyList(vidLocal.Hash));
                    }

                    ReportUpdate(progress, 90);
                    if (cmds.Count > 0)
                        Queue.Instance.AddRange(cmds);
                    ReportFinish(progress);
                }
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing ServerProcessFile: {VideoLocalID} - {ex}", ex);
            }
        }
    }
}