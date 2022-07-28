using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Iesi.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Interfaces;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.AniDB.Http;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Shoko.Server.Utilities;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_SyncMyList)]
    public class CommandRequest_SyncMyList : CommandRequestImplementation
    {
        public bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority7;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.SyncMyList, extraParams = new string[0]};

        public CommandRequest_SyncMyList()
        {
        }

        public CommandRequest_SyncMyList(bool forced)
        {
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            return;
            logger.Info("Processing CommandRequest_SyncMyList");
            var handler = serviceProvider.GetRequiredService<IHttpConnectionHandler>();

            try
            {
                // we will always assume that an anime was downloaded via http first
                var sched = RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMyListSync);
                if (sched == null)
                {
                    sched = new ScheduledUpdate { UpdateType = (int)ScheduledUpdateType.AniDBMyListSync, UpdateDetails = string.Empty };
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.Instance.AniDb.MyList_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                // Get the list from AniDB
                var request = new RequestMyList { Username = ServerSettings.Instance.AniDb.Username, Password = ServerSettings.Instance.AniDb.Password };
                var response = request.Execute(handler);

                if (response.Response == null)
                {
                    logger.Warn("AniDB did not return a successful code: " + response.Code);
                    return;
                }

                var totalItems = 0;
                var watchedItems = 0;
                var modifiedItems = 0;

                // Add missing files on AniDB
                var onlineFiles = response.Response.Where(a => a.FileID.HasValue).ToLookup(a => a.FileID);
                var onlineEpisodes = response.Response.Where(a => !a.FileID.HasValue && a.AnimeID.HasValue && a.EpisodeID.HasValue).ToLookup(a => (a.AnimeID, a.EpisodeID));
                var dictAniFiles = RepoFactory.AniDB_File.GetAll().ToLookup(a => a.Hash);

                var missingFiles = 0;
                foreach (var vid in RepoFactory.VideoLocal.GetAll()
                    .Where(a => !string.IsNullOrEmpty(a.Hash)).ToList())
                {
                    // Does it have a linked AniFile
                    if (!TryParseFileID(dictAniFiles, vid, onlineFiles))
                    {
                        if (!TryParseEpisode(vid, onlineEpisodes)) continue;
                    }

                    // means we have found a file in our local collection, which is not recorded online
                    if (ServerSettings.Instance.AniDb.MyList_AddFiles)
                    {
                        var cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
                        cmdAddFile.Save();
                    }
                    missingFiles++;
                }
                logger.Info($"MYLIST Missing Files: {missingFiles} Added to queue for inclusion");

                var aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
                var modifiedSeries = new LinkedHashSet<SVR_AnimeSeries>();

                // Remove Missing Files and update watched states (single loop)
                var filesToRemove = new List<IHash>();
                var myListIDsToRemove = new List<int>();
                foreach (var myitem in response.Response)
                {
                    try
                    {
                        totalItems++;
                        if (myitem.ViewedAt.HasValue) watchedItems++;

                        var hash = string.Empty;

                        var anifile = myitem.FileID == null ? null : RepoFactory.AniDB_File.GetByFileID(myitem.FileID.Value);
                        if (anifile != null)
                        {
                            hash = anifile.Hash;
                        }
                        else
                        {
                            // look for manually linked files
                            var xrefs = myitem.EpisodeID == null ? null :
                                RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID.Value);
                            foreach (var xref in xrefs)
                            {
                                if (xref.CrossRefSource == (int) CrossRefSource.AniDB) continue;
                                hash = xref.Hash;
                                break;
                            }
                        }

                        // We couldn't evem find a hash, so remove it
                        if (string.IsNullOrEmpty(hash) && myitem.MyListID.HasValue)
                        {
                            myListIDsToRemove.Add(myitem.MyListID.Value);
                            continue;
                        }

                        // If there's no video local, we don't have it
                        var vl = RepoFactory.VideoLocal.GetByHash(hash);
                        if (vl == null)
                        {
                            filesToRemove.Add(vl);
                            continue;
                        }

                        foreach (var juser in aniDBUsers)
                        {
                            var localStatus = false;

                            // doesn't matter which anidb user we use
                            var jmmUserID = juser.JMMUserID;
                            var userRecord = vl.GetUserRecord(juser.JMMUserID);
                            if (userRecord != null) localStatus = userRecord.WatchedDate.HasValue;

                            var action = string.Empty;
                            if (localStatus == myitem.ViewedAt.HasValue) continue;

                            // localStatus and AniDB Status are different
                            DateTime? watchedDate = myitem.ViewedAt ?? DateTime.Now;
                            if (localStatus)
                            {
                                // local = watched, anidb = unwatched
                                if (ServerSettings.Instance.AniDb.MyList_ReadUnwatched)
                                {
                                    modifiedItems++;
                                    vl.ToggleWatchedStatus(false, false, watchedDate,
                                        false, jmmUserID, false,
                                        true);
                                    action = "Used AniDB Status";
                                }
                                else if (ServerSettings.Instance.AniDb.MyList_SetWatched)
                                {
                                    vl.ToggleWatchedStatus(true, true, userRecord.WatchedDate, false, jmmUserID,
                                        false, true);
                                }
                            }
                            else
                            {
                                // means local is un-watched, and anidb is watched
                                if (ServerSettings.Instance.AniDb.MyList_ReadWatched)
                                {
                                    modifiedItems++;
                                    vl.ToggleWatchedStatus(true, false, watchedDate, false,
                                        jmmUserID, false, true);
                                    action = "Updated Local record to Watched";
                                }
                                else if (ServerSettings.Instance.AniDb.MyList_SetUnwatched)
                                {
                                    vl.ToggleWatchedStatus(false, true, watchedDate, false, jmmUserID,
                                        false, true);
                                }
                            }

                            vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null)
                                .DistinctBy(a => a.AnimeSeriesID).ForEach(a => modifiedSeries.Add(a));
                            logger.Info(
                                $"MYLISTDIFF:: File {vl.FileName} - Local Status = {localStatus}, AniDB Status = {myitem.ViewedAt.HasValue} --- {action}");
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.Error($"A MyList Item threw an error while syncing: {ex}");
                    }
                }

                // Actually remove the files
                if (filesToRemove.Count > 0)
                {
                    foreach (var vl in filesToRemove)
                    {
                        var deleteCommand = new CommandRequest_DeleteFileFromMyList(vl.ED2KHash, vl.FileSize);
                        deleteCommand.Save();
                    }
                }
                
                if (myListIDsToRemove.Count > 0)
                {
                    foreach (var lid in myListIDsToRemove)
                    {
                        // TODO MyListID version
                        //var deleteCommand = new CommandRequest_DeleteFileFromMyList();
                        //deleteCommand.Save();
                    }
                }
                
                if (myListIDsToRemove.Count + filesToRemove.Count > 0)
                    logger.Info($"MYLIST Missing Files: {myListIDsToRemove.Count + filesToRemove.Count} added to queue for deletion");

                modifiedSeries.ForEach(a => a.QueueUpdateStats());

                logger.Info($"Process MyList: {totalItems} Items, {missingFiles} Added, {filesToRemove.Count} Deleted, {watchedItems} Watched, {modifiedItems} Modified");

                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_SyncMyList: {Ex} ", ex);
            }
        }

        private static bool TryParseFileID(ILookup<string, SVR_AniDB_File> dictAniFiles, SVR_VideoLocal vid, ILookup<int?, ResponseMyList> onlineFiles)
        {
            if (!dictAniFiles.Contains(vid.Hash)) return false;

            var fileID = dictAniFiles[vid.Hash].FirstOrDefault()?.FileID ?? 0;
            if (fileID == 0) return false;
            // Is it in MyList
            if (!onlineFiles.Contains(fileID)) return false;
            var file = onlineFiles[fileID].FirstOrDefault(a => a != null);

            if (file == null) return false;
            if (vid.MyListID == 0 && file.MyListID.HasValue)
            {
                vid.MyListID = file.MyListID.Value;
                RepoFactory.VideoLocal.Save(vid);
            }

            // Update file state if deleted
            if ((int)file.State == (int)ServerSettings.Instance.AniDb.MyList_StorageState) return true;

            var seconds = Commons.Utils.AniDB.GetAniDBDateAsSeconds(file.ViewedAt);
            var cmdUpdateFile = new CommandRequest_UpdateMyListFileStatus(vid.Hash, file.ViewedAt.HasValue, false, seconds);
            cmdUpdateFile.Save();

            return true;
        }

        private static bool TryParseEpisode(SVR_VideoLocal vid, ILookup<(int? AnimeID, int? EpisodeID),ResponseMyList> onlineEpisodes)
        {

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_SyncMyList";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest
            {
                CommandID = CommandID,
                CommandType = CommandType,
                Priority = Priority,
                CommandDetails = ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}
