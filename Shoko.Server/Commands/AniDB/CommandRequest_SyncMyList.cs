﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using AniDBAPI;
using AniDBAPI.Commands;
using Iesi.Collections.Generic;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_SyncMyList : CommandRequestImplementation, ICommandRequest
    {
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public QueueStateStruct PrettyDescription => new QueueStateStruct {queueState = QueueStateEnum.SyncMyList, extraParams = new string[0]};

        public CommandRequest_SyncMyList()
        {
        }

        public CommandRequest_SyncMyList(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int) CommandRequestType.AniDB_SyncMyList;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_SyncMyList");

            try
            {
                // we will always assume that an anime was downloaded via http first
                ScheduledUpdate sched =
                    RepoFactory.ScheduledUpdate.GetByUpdateType((int) ScheduledUpdateType.AniDBMyListSync);
                if (sched == null)
                {
                    sched = new ScheduledUpdate
                    {
                        UpdateType = (int)ScheduledUpdateType.AniDBMyListSync,
                        UpdateDetails = ""
                    };
                }
                else
                {
                    int freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    TimeSpan tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                // Get the list from AniDB
                AniDBHTTPCommand_GetMyList cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                enHelperActivityType ev = cmd.Process();
                if (ev != enHelperActivityType.GotMyListHTTP)
                {
                    logger.Warn("AniDB did not return a successful code: " + ev);
                    return;
                }

                int totalItems = 0;
                int watchedItems = 0;
                int modifiedItems = 0;
                double pct = 0;

                // Add missing files on AniDB
                Dictionary<int, Raw_AniDB_MyListFile> onlineFiles = new Dictionary<int, Raw_AniDB_MyListFile>();
                foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
                    onlineFiles[myitem.FileID] = myitem;

                Dictionary<string, SVR_AniDB_File> dictAniFiles = new Dictionary<string, SVR_AniDB_File>();
                IReadOnlyList<SVR_AniDB_File> allAniFiles = RepoFactory.AniDB_File.GetAll();
                foreach (SVR_AniDB_File anifile in allAniFiles)
                    dictAniFiles[anifile.Hash] = anifile;

                int missingFiles = 0;
                foreach (SVR_VideoLocal vid in RepoFactory.VideoLocal.GetAll()
                    .Where(a => !string.IsNullOrEmpty(a.Hash)).ToList())
                {
                    // Does it have a linked AniFile
                    if (!dictAniFiles.ContainsKey(vid.Hash)) continue;

                    int fileID = dictAniFiles[vid.Hash].FileID;
                    // Is it in MyList
                    if (onlineFiles.ContainsKey(fileID))
                    {
                        Raw_AniDB_MyListFile file = onlineFiles[fileID];
                        
                        // Update file state if deleted
                        if (file.State == (int) AniDBFile_State.Deleted)
                        {
                            CommandRequest_UpdateMyListFileStatus cmdUpdateFile = 
                                new CommandRequest_UpdateMyListFileStatus(vid.Hash, file.WatchedDate.HasValue, false, 0);
                            cmdUpdateFile.Save();
                        }
                    };

                    // means we have found a file in our local collection, which is not recorded online
                    CommandRequest_AddFileToMyList cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
                    cmdAddFile.Save();
                    missingFiles++;
                }
                logger.Info($"MYLIST Missing Files: {missingFiles} Added to queue for inclusion");

                List<SVR_JMMUser> aniDBUsers = RepoFactory.JMMUser.GetAniDBUsers();
                LinkedHashSet<SVR_AnimeSeries> modifiedSeries = new LinkedHashSet<SVR_AnimeSeries>();

                // Remove Missing Files and update watched states (single loop)
                List<int> filesToRemove = new List<int>();
                foreach (Raw_AniDB_MyListFile myitem in cmd.MyListItems)
                {
                    // ignore files mark as deleted by the user
                    if (myitem.State == (int) AniDBFile_State.Deleted) continue;

                    totalItems++;
                    if (myitem.IsWatched) watchedItems++;

                    //calculate percentage
                    pct = totalItems / (double)cmd.MyListItems.Count * 100;
                    string spct = pct.ToString("#0.0");

                    string hash = string.Empty;

                    SVR_AniDB_File anifile = RepoFactory.AniDB_File.GetByFileID(myitem.FileID);
                    if (anifile != null)
                    {
                        hash = anifile.Hash;
                    }
                    else
                    {
                        // look for manually linked files
                        List<CrossRef_File_Episode> xrefs =
                            RepoFactory.CrossRef_File_Episode.GetByEpisodeID(myitem.EpisodeID);
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
                        filesToRemove.Add(myitem.FileID);
                        continue;
                    }

                    // If there's no video local, we don't have it
                    SVR_VideoLocal vl = RepoFactory.VideoLocal.GetByHash(hash);
                    if (vl == null)
                    {
                        filesToRemove.Add(myitem.FileID);
                        continue;
                    }

                    foreach (SVR_JMMUser juser in aniDBUsers)
                    {
                        bool localStatus = false;

                        // doesn't matter which anidb user we use
                        int jmmUserID = juser.JMMUserID;
                        VideoLocal_User userRecord = vl.GetUserRecord(juser.JMMUserID);
                        if (userRecord != null) localStatus = userRecord.WatchedDate.HasValue;

                        string action = "";
                        if (localStatus == myitem.IsWatched) continue;

                        // localStatus and AniDB Status are different
                        DateTime? watchedDate = myitem.WatchedDate ?? DateTime.Now;
                        if (localStatus)
                        {
                            // local = watched, anidb = unwatched
                            if (ServerSettings.AniDB_MyList_ReadUnwatched)
                            {
                                modifiedItems++;
                                vl.ToggleWatchedStatus(false, false, watchedDate,
                                    false, false, jmmUserID, false,
                                    true);
                                action = "Used AniDB Status";
                            }
                            else if (ServerSettings.AniDB_MyList_SetWatched)
                            {
                                vl.ToggleWatchedStatus(true, true, userRecord.WatchedDate, false, false, jmmUserID,
                                    false, true);
                            }
                        }
                        else
                        {
                            // means local is un-watched, and anidb is watched
                            if (ServerSettings.AniDB_MyList_ReadWatched)
                            {
                                modifiedItems++;
                                vl.ToggleWatchedStatus(true, false, watchedDate, false, false,
                                    jmmUserID, false, true);
                                action = "Updated Local record to Watched";
                            }
                            else if (ServerSettings.AniDB_MyList_SetUnwatched)
                            {
                                vl.ToggleWatchedStatus(false, true, watchedDate, false, false, jmmUserID,
                                    false, true);
                            }
                        }
                        vl.GetAnimeEpisodes().Select(a => a.GetAnimeSeries()).Where(a => a != null).ForEach(a => modifiedSeries.Add(a));
                        logger.Info($"MYLISTDIFF:: File {vl.FileName} - Local Status = {localStatus}, AniDB Status = {myitem.IsWatched} --- {action}");
                    }
                }

                // Actually remove the files
                if (filesToRemove.Count > 0)
                {
                    foreach (int fileID in filesToRemove)
                    {
                        CommandRequest_DeleteFileFromMyList deleteCommand =
                            new CommandRequest_DeleteFileFromMyList(fileID);
                        deleteCommand.Save();
                    }
                    logger.Info($"MYLIST Missing Files: {filesToRemove.Count} Added to queue for deletion");
                }

                modifiedSeries.ForEach(a => a.QueueUpdateStats());

                logger.Info($"Process MyList: {totalItems} Items, {missingFiles} Added, {filesToRemove.Count} Deleted, {watchedItems} Watched, {modifiedItems} Modified");

                sched.LastUpdate = DateTime.Now;
                RepoFactory.ScheduledUpdate.Save(sched);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_SyncMyList: {0} ", ex.Message);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_SyncMyList";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            CommandType = cq.CommandType;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
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