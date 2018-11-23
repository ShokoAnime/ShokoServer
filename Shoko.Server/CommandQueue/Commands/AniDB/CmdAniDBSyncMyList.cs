using System;
using System.Collections.Generic;
using System.Linq;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Commands;
using Shoko.Server.Providers.AniDB.Raws;
using Shoko.Server.Repositories;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBSyncMyList : BaseCommand, ICommand
    {


        public bool ForceRefresh { get; set; }

        public string ParallelTag { get; set; } = WorkTypes.AniDB;
        public int ParallelMax { get; set; } = 1;
        public int Priority { get; set; } = 7;
        public string Id => "SyncMyList";
        public QueueStateStruct PrettyDescription => new QueueStateStruct {QueueState = QueueStateEnum.SyncMyList, ExtraParams = new [] { ForceRefresh.ToString()}};
        public string WorkType => WorkTypes.AniDB;
        public CmdAniDBSyncMyList(string str) : base(str)
        {
        }

        public CmdAniDBSyncMyList(bool forced)
        {
            ForceRefresh = forced;
        }
        public override void Run(IProgress<ICommand> progress = null)
        {
            logger.Info("Processing CommandRequest_SyncMyList");

            try
            {
                ReportInit(progress);
                // we will always assume that an anime was downloaded via http first
                ScheduledUpdate sched = Repo.Instance.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMyListSync);
                ReportUpdate(progress,15);
                if (sched != null)
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyList_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh)
                        {
                            ReportFinish(progress);
                            return;
                        }
                    }
                }

                // Get the list from AniDB
                AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.Instance.AniDb.Username, ServerSettings.Instance.AniDb.Password);
                ReportUpdate(progress,30);

                enHelperActivityType ev = cmd.Process();
                if (ev != enHelperActivityType.GotMyListHTTP)
                {
                    logger.Warn("AniDB did not return a successful code: " + ev);
                    ReportFinish(progress);
                    return;
                }

                int totalItems = 0;
                int watchedItems = 0;
                int modifiedItems = 0;

                // Add missing files on AniDB
                var onlineFiles = cmd.MyListItems.ToLookup(a => a.FileID);
                var dictAniFiles = Repo.Instance.AniDB_File.GetAll().ToLookup(a => a.Hash);
                ReportUpdate(progress,45);
                int missingFiles = 0;
                List<ICommand> updates=new List<ICommand>();
                foreach (SVR_VideoLocal vid in Repo.Instance.VideoLocal.GetAll().Where(a => !string.IsNullOrEmpty(a.Hash)).ToList())
                {
                    // Does it have a linked AniFile
                    if (!dictAniFiles.Contains(vid.Hash)) continue;

                    int fileID = dictAniFiles[vid.Hash].FirstOrDefault()?.FileID ?? 0;
                    if (fileID == 0) continue;
                    // Is it in MyList
                    if (onlineFiles.Contains(fileID))
                    {
                        Raw_AniDB_MyListFile file = onlineFiles[fileID].FirstOrDefault(a => a != null);

                        if (file != null)
                        {
                            if (vid.MyListID == 0)
                            {
                                using (var upd = Repo.Instance.VideoLocal.BeginAddOrUpdate(vid))
                                {
                                    upd.Entity.MyListID = file.ListID;
                                    upd.Commit();
                                }
                            }

                            // Update file state if deleted
                            if (file.State != (int) ServerSettings.Instance.AniDb.MyList_StorageState)
                            {
                                int seconds = Commons.Utils.AniDB.GetAniDBDateAsSeconds(file.WatchedDate);
                                updates.Add(new CmdAniDBUpdateMyListFileStatus(vid.Hash, file.WatchedDate.HasValue,false,seconds));
                            }

                            continue;
                        }
                    }
                    // means we have found a file in our local collection, which is not recorded online
                    if (ServerSettings.Instance.AniDb.MyList_AddFiles)
                    {
                        updates.Add(new CmdAniDBAddFileToMyList(vid.Hash));
                    }

                    missingFiles++;
                }
                ReportUpdate(progress,55);
                if (updates.Count > 0)
                    Queue.Instance.AddRange(updates);

                ReportUpdate(progress,60);
                logger.Info($"MYLIST Missing Files: {missingFiles} Added to queue for inclusion");

                List<SVR_JMMUser> aniDBUsers = Repo.Instance.JMMUser.GetAniDBUsers();
                HashSet<SVR_AnimeSeries> modifiedSeries = new HashSet<SVR_AnimeSeries>();

                // Remove Missing Files and update watched states (single loop)
                List<int> filesToRemove = new List<int>();
                foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
                {
                    try
                    {
                        totalItems++;
                        if (myitem.IsWatched) watchedItems++;

                        string hash = string.Empty;

                        SVR_AniDB_File anifile = Repo.Instance.AniDB_File.GetByID(myitem.FileID);
                        if (anifile != null)
                        {
                            hash = anifile.Hash;
                        }
                        else
                        {
                            // look for manually linked files
                            List<CrossRef_File_Episode> xrefs = Repo.Instance.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID);
                            foreach (CrossRef_File_Episode xref in xrefs)
                            {
                                if (xref.CrossRefSource == (int) CrossRefSource.AniDB) continue;
                                hash = xref.Hash;
                                break;
                            }
                        }

                        // We couldn't evem find a hash, so remove it
                        if (string.IsNullOrEmpty(hash))
                        {
                            filesToRemove.Add(myitem.ListID);
                            continue;
                        }

                        // If there's no video local, we don't have it
                        SVR_VideoLocal vl = Repo.Instance.VideoLocal.GetByHash(hash);
                        if (vl == null)
                        {
                            filesToRemove.Add(myitem.ListID);
                            continue;
                        }

                        foreach (SVR_JMMUser juser in aniDBUsers)
                        {
                            bool localStatus = false;

                            // doesn't matter which anidb user we use
                            int jmmUserID = juser.JMMUserID;
                            VideoLocal_User userRecord = vl.GetUserRecord(juser.JMMUserID);
                            if (userRecord != null) localStatus = userRecord.WatchedDate.HasValue;

                            string action = string.Empty;
                            if (localStatus == myitem.IsWatched) continue;

                            // localStatus and AniDB Status are different
                            DateTime? watchedDate = myitem.WatchedDate ?? DateTime.Now;
                            if (localStatus)
                            {
                                // local = watched, anidb = unwatched
                                if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched)
                                {
                                    modifiedItems++;
                                    vl.ToggleWatchedStatus(false, false, watchedDate, false, jmmUserID, false, true);
                                    action = "Used AniDB Status";
                                }
                                else if (ServerSettings.Instance.AniDb.MyList_SetWatched)
                                {
                                    vl.ToggleWatchedStatus(true, true, userRecord.WatchedDate, false, jmmUserID, false, true);
                                }
                            }
                            else
                            {
                                // means local is un-watched, and anidb is watched
                                if (ServerSettings.Instance.AniDb.MyList_ReadWatched)
                                {
                                    modifiedItems++;
                                    vl.ToggleWatchedStatus(true, false, watchedDate, false, jmmUserID, false, true);
                                    action = "Updated Local record to Watched";
                                }
                                else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched)
                                {
                                    vl.ToggleWatchedStatus(false, true, watchedDate, false, jmmUserID, false, true);
                                }
                            }

                            vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null).DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
                            logger.Info($"MYLISTDIFF:: File {vl.Info} - Local Status = {localStatus}, AniDB Status = {myitem.IsWatched} --- {action}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"A MyList Item threw an error while syncing: {ex}");
                    }
                }

                ReportUpdate(progress,80);
                // Actually remove the files
                if (filesToRemove.Count > 0)
                {
                    Queue.Instance.AddRange(filesToRemove.Select(a => new CmdAniDBDeleteFileFromMyList(a)).ToList());
                    logger.Info($"MYLIST Missing Files: {filesToRemove.Count} Added to queue for deletion");
                }

                ReportUpdate(progress,90);
                modifiedSeries.ForEach(a => a.QueueUpdateStats());

                logger.Info($"Process MyList: {totalItems} Items, {missingFiles} Added, {filesToRemove.Count} Deleted, {watchedItems} Watched, {modifiedItems} Modified");

                using (var upd = Repo.Instance.ScheduledUpdate.BeginAddOrUpdate(sched, () => new ScheduledUpdate {UpdateType = (int) ScheduledUpdateType.AniDBMyListSync, UpdateDetails = string.Empty}))
                {
                    sched.LastUpdate = DateTime.Now;
                    upd.Commit();
                }

                ReportFinish(progress);
            }
            catch (Exception ex)
            {
                ReportError(progress, $"Error processing Command AniDb.SyncMyList: {ex}", ex);
            }
        }
    }
}