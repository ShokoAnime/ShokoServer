using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.CommandQueue.Commands.Trakt;
using Shoko.Server.Commands;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.CommandQueue.Commands.AniDB
{
    public class CmdAniDBAddFileToMyList : BaseCommand<CmdAniDBAddFileToMyList>, ICommand
    {
        public SVR_VideoLocal VideoLocal { get; set; }
        public bool ReadStates { get; set; }
        public int Priority { get; set; } = 6;
        public string Id => $"AddFileToMyList_{VideoLocal.Hash}";
        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.AniDB_MyListAdd, extraParams = new[] {VideoLocal.Info}};
        public WorkTypes WorkType => WorkTypes.AniDB;
        public string ParallelTag { get; set; } = WorkTypes.AniDB.ToString();
        public int ParallelMax { get; set; } = 1;

        public CmdAniDBAddFileToMyList(SVR_VideoLocal vlocal, bool readstates=true)
        {
            VideoLocal = vlocal;
            ReadStates = readstates;
        }

        public CmdAniDBAddFileToMyList(string hash, bool readStates = true)
        {
            VideoLocal = Repo.Instance.VideoLocal.GetByHash(hash);
            ReadStates = readStates;
        }
        public CmdAniDBAddFileToMyList(string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                InternalSerialize hf = JsonConvert.DeserializeObject<InternalSerialize>(str,JsonSettings);
                SVR_VideoLocal vid = Repo.Instance.VideoLocal.GetByHash(hf.Hash);
                if (vid != null)
                {
                    VideoLocal = vid;
                    ReadStates = hf.ReadStates;
                }
            }
        }



        public override string Serialize()
        {
            string str = null;
            if (VideoLocal?.Hash != null)
            {
                InternalSerialize hf = new InternalSerialize { Hash = VideoLocal.Hash, ReadStates = ReadStates };
                str = JsonConvert.SerializeObject(hf,JsonSettings);
            }
            return str;
        }



        private class InternalSerialize
        {
            public string Hash { get; set; }
            public bool ReadStates { get; set; }
        }
        public override CommandResult Run(IProgress<ICommandProgress> progress = null)
        {
            logger.Info($"Processing CommandRequest_AddFileToMyList: {VideoLocal.Info} - {VideoLocal?.Hash} - {ReadStates}");
            try
            {
                if (VideoLocal == null) return ReportErrorAndGetResult(progress, CommandResultStatus.Error, "Videlocal not found");
                InitProgress(progress);

                // when adding a file via the API, newWatchedStatus will return with current watched status on AniDB
                // if the file is already on the user's list

                bool isManualLink = false;
                List<CrossRef_File_Episode> xrefs = VideoLocal.EpisodeCrossRefs.DistinctBy(a => Tuple.Create(a.AnimeID, a.EpisodeID)).ToList();
                if (xrefs.Count > 0)
                    isManualLink = xrefs[0].CrossRefSource != (int) CrossRefSource.AniDB;

                // mark the video file as watched
                List<SVR_JMMUser> aniDBUsers = Repo.Instance.JMMUser.GetAniDBUsers();
                SVR_JMMUser juser = aniDBUsers.FirstOrDefault();
                DateTime? originalWatchedDate = null;
                if (juser != null)
                    originalWatchedDate = VideoLocal.GetUserRecord(juser.JMMUserID)?.WatchedDate?.ToUniversalTime();

                DateTime? newWatchedDate = null;
                int? lid = null;
                // this only gets overwritten if the response is File Already in MyList
                AniDBFile_State? state = ServerSettings.Instance.AniDb.MyList_StorageState;

                if (isManualLink)
                    foreach (var xref in xrefs)
                        (lid, newWatchedDate) = ShokoService.AnidbProcessor.AddFileToMyList(xref.AnimeID, xref.GetEpisode().EpisodeNumber, originalWatchedDate, ref state);
                else
                    (lid, newWatchedDate) = ShokoService.AnidbProcessor.AddFileToMyList(VideoLocal, originalWatchedDate, ref state);
                UpdateAndReportProgress(progress,15);
                // never true for Manual Links, so no worries about the loop overwriting it
                if (lid != null && lid.Value > 0)
                {
                    using (var upd = Repo.Instance.VideoLocal.BeginAddOrUpdate(() => VideoLocal)) //TODO: Test if this will work{
                    {
                        upd.Entity.MyListID = lid.Value;
                        upd.Commit();
                    }
                }

                UpdateAndReportProgress(progress,30);
                logger.Info($"Added File to MyList. File: {VideoLocal.Info}  Manual Link: {isManualLink}  Watched Locally: {originalWatchedDate != null}  Watched AniDB: {newWatchedDate != null}  Local State: {ServerSettings.Instance.AniDb.MyList_StorageState}  AniDB State: {state}  ReadStates: {ReadStates}  ReadWatched Setting: {ServerSettings.Instance.AniDb.MyList_ReadWatched}  ReadUnwatched Setting: {ServerSettings.Instance.AniDb.MyList_ReadUnwatched}");
                if (juser != null)
                {
                    bool watched = newWatchedDate != null;

                    bool watchedLocally = originalWatchedDate != null;
                    bool watchedChanged = watched != watchedLocally;

                    if (ReadStates)
                    {
                        // handle import watched settings. Don't update AniDB in either case, we'll do that with the storage state
                        if (ServerSettings.Instance.AniDb.MyList_ReadWatched && watched && !watchedLocally)
                        {
                            VideoLocal.ToggleWatchedStatus(true, false, newWatchedDate, false, juser.JMMUserID, false, false);
                        }
                        else if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched && !watched && watchedLocally)
                        {
                            VideoLocal.ToggleWatchedStatus(false, false, null, false, juser.JMMUserID, false, false);
                        }
                    }

                    UpdateAndReportProgress(progress,45);

                    // We should have a MyListID at this point, so hopefully this will prevent looping
                    if (watchedChanged || state != ServerSettings.Instance.AniDb.MyList_StorageState)
                    {
                        // if VideoLocal.MyListID > 0, isManualLink _should_ always be false, but _should_ isn't good enough
                        if (VideoLocal.MyListID > 0 && !isManualLink)
                        {
                            if (ServerSettings.Instance.AniDb.MyList_SetWatched && watchedLocally)
                                ShokoService.AnidbProcessor.UpdateMyListFileStatus(VideoLocal, true, originalWatchedDate);
                            else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched && !watchedLocally)
                                ShokoService.AnidbProcessor.UpdateMyListFileStatus(VideoLocal, false);
                        }
                        else if (isManualLink)
                        {
                            foreach (var xref in xrefs)
                            {
                                if (ServerSettings.Instance.AniDb.MyList_SetWatched && watchedLocally)
                                    ShokoService.AnidbProcessor.UpdateMyListFileStatus(VideoLocal, xref.AnimeID, xref.GetEpisode().EpisodeNumber, true, originalWatchedDate);
                                else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched && !watchedLocally)
                                    ShokoService.AnidbProcessor.UpdateMyListFileStatus(VideoLocal, xref.AnimeID, xref.GetEpisode().EpisodeNumber, false);
                            }
                        }
                    }

                    UpdateAndReportProgress(progress,60);
                }

                // if we don't have xrefs, then no series or eps.
                if (xrefs.Count <= 0)
                {
                    return ReportFinishAndGetResult(progress);

                }

                SVR_AnimeSeries ser = Repo.Instance.AnimeSeries.GetByAnimeID(xrefs[0].AnimeID);
                // all the eps should belong to the same anime
                ser.QueueUpdateStats();
                //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                UpdateAndReportProgress(progress,75);

                // lets also try adding to the users trakt collecion
                if (ServerSettings.Instance.TraktTv.Enabled && !string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken))
                {
                    foreach (SVR_AnimeEpisode aep in VideoLocal.GetAnimeEpisodes())
                    {
                        //TODO COMMANDS
                        CommandQueue.Queue.Instance.Add(new CmdTraktCollectionEpisode(aep.AnimeEpisodeID, TraktSyncAction.Add));
                    }
                }
                return ReportFinishAndGetResult(progress);
            }
            catch (Exception ex)
            {
                return ReportErrorAndGetResult(progress, CommandResultStatus.Error, $"Error processing Command AniDB.AddFileToMyList: {VideoLocal?.Hash} - {ex}", ex);
            }
        }
    }
}