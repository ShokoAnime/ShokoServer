using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Shoko.Models.Server;
using Shoko.Server.Commands.AniDB;
using NutzCode.CloudFileSystem;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_ProcessFile : CommandRequestImplementation, ICommandRequest
    {
        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        private SVR_VideoLocal vlocal = null;

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority3; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null)
                    return new QueueStateStruct()
                    {
                        queueState = QueueStateEnum.FileInfo,
                        extraParams = new string[] {vlocal.FileName}
                    };
                else
                    return new QueueStateStruct()
                    {
                        queueState = QueueStateEnum.FileInfo,
                        extraParams = new string[] {VideoLocalID.ToString()}
                    };
            }
        }

        public CommandRequest_ProcessFile()
        {
        }

        public CommandRequest_ProcessFile(int vidLocalID, bool forceAniDB)
        {
            this.VideoLocalID = vidLocalID;
            this.ForceAniDB = forceAniDB;
            this.CommandType = (int) CommandRequestType.ProcessFile;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Trace("Processing File: {0}", VideoLocalID);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            try
            {
                vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
                if (vlocal == null) return;

                //now that we have all the has info, we can get the AniDB Info
                ProcessFile_AniDB(vlocal);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_ProcessFile: {0} - {1}", VideoLocalID, ex.ToString());
                return;
            }

            // TODO update stats for group and series

            // TODO check for TvDB
        }

        private void ProcessFile_AniDB(SVR_VideoLocal vidLocal)
        {
            logger.Trace("Checking for AniDB_File record for: {0} --- {1}", vidLocal.Hash, vidLocal.FileName);
            // check if we already have this AniDB_File info in the database

            lock (vidLocal)
            {
                SVR_AniDB_File aniFile = null;

                if (!ForceAniDB)
                {
                    aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, vlocal.FileSize);

                    if (aniFile == null)
                        logger.Trace("AniDB_File record not found");
                }

                int animeID = 0;

                if (aniFile == null)
                {
                    // get info from AniDB
                    logger.Debug("Getting AniDB_File record from AniDB....");
                    Raw_AniDB_File fileInfo = ShokoService.AnidbProcessor.GetFileInfo(vidLocal);
                    if (fileInfo != null)
                    {
                        // check if we already have a record
                        aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, vlocal.FileSize);

                        if (aniFile == null)
                            aniFile = new SVR_AniDB_File();

                        SVR_AniDB_File.Populate(aniFile, fileInfo);

                        //overwrite with local file name
                        string localFileName = vidLocal.FileName;
                        aniFile.FileName = localFileName;

                        RepoFactory.AniDB_File.Save(aniFile, false);
                        aniFile.CreateLanguages();
                        aniFile.CreateCrossEpisodes(localFileName);

                        if (!string.IsNullOrEmpty(fileInfo.OtherEpisodesRAW))
                        {
                            string[] epIDs = fileInfo.OtherEpisodesRAW.Split(',');
                            foreach (string epid in epIDs)
                            {
                                if (int.TryParse(epid, out int id))
                                {
                                    CommandRequest_GetEpisode cmdEp = new CommandRequest_GetEpisode(id);
                                    cmdEp.Save();
                                }
                            }
                        }

                        animeID = aniFile.AnimeID;
                    }
                }

                bool missingEpisodes = false;

                // if we still haven't got the AniDB_File Info we try the web cache or local records
                if (aniFile == null)
                {
                    // check if we have any records from previous imports
                    List<CrossRef_File_Episode> crossRefs = RepoFactory.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
                    if (crossRefs == null || crossRefs.Count == 0)
                    {
                        // lets see if we can find the episode/anime info from the web cache
                        if (ServerSettings.WebCache_XRefFileEpisode_Get)
                        {
                            List<Shoko.Models.Azure.Azure_CrossRef_File_Episode> xrefs =
                                AzureWebAPI.Get_CrossRefFileEpisode(vidLocal);

                            crossRefs = new List<CrossRef_File_Episode>();
                            if (xrefs == null || xrefs.Count == 0)
                            {
                                logger.Debug(
                                    "Cannot find AniDB_File record or get cross ref from web cache record so exiting: {0}",
                                    vidLocal.ED2KHash);
                                return;
                            }
                            else
                            {
                                foreach (Shoko.Models.Azure.Azure_CrossRef_File_Episode xref in xrefs)
                                {
                                    CrossRef_File_Episode xrefEnt = new CrossRef_File_Episode
                                    {
                                        Hash = vidLocal.ED2KHash,
                                        FileName = vidLocal.FileName,
                                        FileSize = vidLocal.FileSize,
                                        CrossRefSource = (int)CrossRefSource.WebCache,
                                        AnimeID = xref.AnimeID,
                                        EpisodeID = xref.EpisodeID,
                                        Percentage = xref.Percentage,
                                        EpisodeOrder = xref.EpisodeOrder
                                    };
                                    bool duplicate = false;

                                    foreach (CrossRef_File_Episode xrefcheck in crossRefs)
                                    {
                                        if (xrefcheck.AnimeID == xrefEnt.AnimeID &&
                                            xrefcheck.EpisodeID == xrefEnt.EpisodeID &&
                                            xrefcheck.Hash == xrefEnt.Hash)
                                            duplicate = true;
                                    }

                                    if (!duplicate)
                                    {
                                        crossRefs.Add(xrefEnt);
                                        // in this case we need to save the cross refs manually as AniDB did not provide them
                                        RepoFactory.CrossRef_File_Episode.Save(xrefEnt);
                                    }
                                }
                            }
                        }
                        else
                        {
                            logger.Debug("Cannot get AniDB_File record so exiting: {0}", vidLocal.ED2KHash);
                            return;
                        }
                    }

                    // we assume that all episodes belong to the same anime
                    foreach (CrossRef_File_Episode xref in crossRefs)
                    {
                        animeID = xref.AnimeID;

                        AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
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
                        logger.Debug("Could not find any cross ref records for: {0}", vidLocal.ED2KHash);
                        missingEpisodes = true;
                    }
                    else
                    {
                        foreach (CrossRef_File_Episode xref in aniFile.EpisodeCrossRefs)
                        {
                            AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                            if (ep == null)
                                missingEpisodes = true;

                            animeID = xref.AnimeID;
                        }
                    }
                }

                // get from DB
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                bool animeRecentlyUpdated = false;

                if (anime != null)
                {
                    TimeSpan ts = DateTime.Now - anime.DateTimeUpdated;
                    if (ts.TotalHours < 4) animeRecentlyUpdated = true;
                }

                // even if we are missing episode info, don't get data  more than once every 4 hours
                // this is to prevent banning
                if (missingEpisodes && !animeRecentlyUpdated)
                {
                    logger.Debug("Getting Anime record from AniDB....");
                    anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true, ServerSettings.AutoGroupSeries);
                }

                // create the group/series/episode records if needed
                SVR_AnimeSeries ser = null;
                if (anime != null)
                {
                    logger.Debug("Creating groups, series and episodes....");
                    // check if there is an AnimeSeries Record associated with this AnimeID
                    ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);
                    if (ser == null)
                    {
                        // create a new AnimeSeries record
                        ser = anime.CreateAnimeSeriesAndGroup();
                    }


                    ser.CreateAnimeEpisodes();

                    // check if we have any group status data for this associated anime
                    // if not we will download it now
                    if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                    {
                        CommandRequest_GetReleaseGroupStatus cmdStatus =
                            new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                        cmdStatus.Save();
                    }

                    // update stats
                    ser.EpisodeAddedDate = DateTime.Now;
                    RepoFactory.AnimeSeries.Save(ser, false, false);

                    foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                    {
                        grp.EpisodeAddedDate = DateTime.Now;
                        RepoFactory.AnimeGroup.Save(grp, true, false);
                    }

                    if (ServerSettings.FileQualityFilterEnabled)
                    {
                        // We do this inside, as the info will not be available as needed otherwise
                        List<SVR_VideoLocal> videoLocals =
                            aniFile?.EpisodeIDs?.SelectMany(a => RepoFactory.VideoLocal.GetByAniDBEpisodeID(a))
                                .Where(b => b != null)
                                .ToList();
                        if (videoLocals != null)
                        {
                            videoLocals.Sort(FileQualityFilter.CompareTo);
                            List<SVR_VideoLocal> keep = videoLocals
                                .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                                .ToList();
                            foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                            if (!FileQualityFilter.Settings.AllowDeletionOfImportedFiles &&
                                videoLocals.Contains(vidLocal)) videoLocals.Remove(vidLocal);
                            videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                            foreach (SVR_VideoLocal toDelete in videoLocals)
                            {
                                toDelete.Places.ForEach(a => a.RemoveAndDeleteFile());
                            }
                        }
                    }
                }
                vidLocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });


                // update stats for groups and series
                if (ser != null)
                {
                    // update all the groups above this series in the heirarchy
                    ser.QueueUpdateStats();
                    //StatsCache.Instance.UpdateUsingSeries(ser.AnimeSeriesID);
                }


                // Add this file to the users list
                if (ServerSettings.AniDB_MyList_AddFiles)
                {
                    CommandRequest_AddFileToMyList cmd = new CommandRequest_AddFileToMyList(vidLocal.ED2KHash);
                    cmd.Save();
                }
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = $"CommandRequest_ProcessFile_{VideoLocalID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            this.CommandID = cq.CommandID;
            this.CommandRequestID = cq.CommandRequestID;
            this.CommandType = cq.CommandType;
            this.Priority = cq.Priority;
            this.CommandDetails = cq.CommandDetails;
            this.DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (this.CommandDetails.Trim().Length > 0)
            {
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(this.CommandDetails);

                // populate the fields
                this.VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "VideoLocalID"));
                this.ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "ForceAniDB"));
            }

            return true;
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            CommandRequest cq = new CommandRequest
            {
                CommandID = this.CommandID,
                CommandType = this.CommandType,
                Priority = this.Priority,
                CommandDetails = this.ToXML(),
                DateTimeUpdated = DateTime.Now
            };
            return cq;
        }
    }
}