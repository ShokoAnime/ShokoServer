using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using AniDBAPI.Commands;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_SyncMyList : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_SyncMyList()
        {
        }

        public CommandRequest_SyncMyList(bool forced)
        {
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_SyncMyList;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority6; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SyncMyList);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_SyncMyList");

            try
            {
                // we will always assume that an anime was downloaded via http first
                var repSched = new ScheduledUpdateRepository();
                var repAniFile = new AniDB_FileRepository();
                var repVidLocals = new VideoLocalRepository();

                var sched = repSched.GetByUpdateType((int)ScheduledUpdateType.AniDBMyListSync);
                if (sched == null)
                {
                    sched = new ScheduledUpdate();
                    sched.UpdateType = (int)ScheduledUpdateType.AniDBMyListSync;
                    sched.UpdateDetails = "";
                }
                else
                {
                    var freqHours = Utils.GetScheduledHours(ServerSettings.AniDB_MyList_UpdateFrequency);

                    // if we have run this in the last 24 hours and are not forcing it, then exit
                    var tsLastRun = DateTime.Now - sched.LastUpdate;
                    if (tsLastRun.TotalHours < freqHours)
                    {
                        if (!ForceRefresh) return;
                    }
                }

                var cmd = new AniDBHTTPCommand_GetMyList();
                cmd.Init(ServerSettings.AniDB_Username, ServerSettings.AniDB_Password);
                var ev = cmd.Process();
                if (ev == enHelperActivityType.GotMyListHTTP && cmd.MyListItems.Count > 1)
                {
                    var totalItems = 0;
                    var watchedItems = 0;
                    var modifiedItems = 0;
                    double pct = 0;

                    // 2. find files locally for the user, which are not recorded on anidb
                    //    and then add them to anidb
                    var onlineFiles = new Dictionary<int, Raw_AniDB_MyListFile>();
                    foreach (var myitem in cmd.MyListItems)
                        onlineFiles[myitem.FileID] = myitem;

                    var dictAniFiles = new Dictionary<string, AniDB_File>();
                    var allAniFiles = repAniFile.GetAll();
                    foreach (var anifile in allAniFiles)
                        dictAniFiles[anifile.Hash] = anifile;

                    var missingFiles = 0;
                    foreach (var vid in repVidLocals.GetAll())
                    {
                        if (!dictAniFiles.ContainsKey(vid.Hash)) continue;

                        var fileID = dictAniFiles[vid.Hash].FileID;

                        if (!onlineFiles.ContainsKey(fileID))
                        {
                            // means we have found a file in our local collection, which is not recorded online
                            var cmdAddFile = new CommandRequest_AddFileToMyList(vid.Hash);
                            cmdAddFile.Save();
                            missingFiles++;
                        }
                    }
                    logger.Info(string.Format("MYLIST Missing Files: {0} Added to queue for inclusion", missingFiles));

                    var repUsers = new JMMUserRepository();
                    var aniDBUsers = repUsers.GetAniDBUsers();

                    var repVidUsers = new VideoLocal_UserRepository();
                    var repFileEp = new CrossRef_File_EpisodeRepository();

                    // 1 . sync mylist items
                    foreach (var myitem in cmd.MyListItems)
                    {
                        // ignore files mark as deleted by the user
                        if (myitem.State == (int)AniDBFileStatus.Deleted) continue;

                        totalItems++;
                        if (myitem.IsWatched) watchedItems++;

                        //calculate percentage
                        pct = totalItems / (double)cmd.MyListItems.Count * 100;
                        var spct = pct.ToString("#0.0");

                        var hash = string.Empty;

                        var anifile = repAniFile.GetByFileID(myitem.FileID);
                        if (anifile != null)
                            hash = anifile.Hash;
                        else
                        {
                            // look for manually linked files
                            var xrefs = repFileEp.GetByEpisodeID(myitem.EpisodeID);
                            foreach (var xref in xrefs)
                            {
                                if (xref.CrossRefSource != (int)CrossRefSource.AniDB)
                                {
                                    hash = xref.Hash;
                                    break;
                                }
                            }
                        }


                        if (!string.IsNullOrEmpty(hash))
                        {
                            // find the video associated with this record
                            var vl = repVidLocals.GetByHash(hash);
                            if (vl == null) continue;

                            foreach (var juser in aniDBUsers)
                            {
                                var localStatus = false;
                                int? jmmUserID = null;

                                // doesn't matter which anidb user we use
                                jmmUserID = juser.JMMUserID;
                                var userRecord = vl.GetUserRecord(juser.JMMUserID);
                                if (userRecord != null) localStatus = true;

                                var action = "";
                                if (localStatus != myitem.IsWatched)
                                {
                                    if (localStatus)
                                    {
                                        // local = watched, anidb = unwatched
                                        if (ServerSettings.AniDB_MyList_ReadUnwatched)
                                        {
                                            modifiedItems++;
                                            if (jmmUserID.HasValue)
                                                vl.ToggleWatchedStatus(myitem.IsWatched, false, myitem.WatchedDate,
                                                    false, false, jmmUserID.Value, false, true);
                                            action = "Used AniDB Status";
                                        }
                                    }
                                    else
                                    {
                                        // means local is un-watched, and anidb is watched
                                        if (ServerSettings.AniDB_MyList_ReadWatched)
                                        {
                                            modifiedItems++;
                                            if (jmmUserID.HasValue)
                                                vl.ToggleWatchedStatus(true, false, myitem.WatchedDate, false, false,
                                                    jmmUserID.Value, false, true);
                                            action = "Updated Local record to Watched";
                                        }
                                    }

                                    var msg =
                                        string.Format(
                                            "MYLISTDIFF:: File {0} - Local Status = {1}, AniDB Status = {2} --- {3}",
                                            vl.FullServerPath, localStatus, myitem.IsWatched, action);
                                    logger.Info(msg);
                                }
                            }


                            //string msg = string.Format("MYLIST:: File {0} - Local Status = {1}, AniDB Status = {2} --- {3}",
                            //	vl.FullServerPath, localStatus, myitem.IsWatched, action);
                            //logger.Info(msg);
                        }
                    }


                    // now update all stats
                    Importer.UpdateAllStats();

                    logger.Info("Process MyList: {0} Items, {1} Watched, {2} Modified", totalItems, watchedItems,
                        modifiedItems);

                    sched.LastUpdate = DateTime.Now;
                    repSched.Save(sched);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_SyncMyList: {0} ", ex.ToString());
            }
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_SyncMyList", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = "CommandRequest_SyncMyList";
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}