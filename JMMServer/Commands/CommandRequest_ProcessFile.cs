using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Xml;
using JMMServer.Commands.AniDB;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;
using CrossRef_File_Episode = JMMServer.Entities.CrossRef_File_Episode;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_ProcessFile : CommandRequestImplementation, ICommandRequest
    {
        private VideoLocal vlocal;

        public CommandRequest_ProcessFile()
        {
        }

        public CommandRequest_ProcessFile(int vidLocalID, bool forceAniDB)
        {
            VideoLocalID = vidLocalID;
            ForceAniDB = forceAniDB;
            CommandType = (int)CommandRequestType.ProcessFile;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority3; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                if (vlocal != null)
                    return string.Format(Resources.Command_FileInfo, vlocal.FullServerPath);
                return string.Format(Resources.Command_FileInfo, VideoLocalID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing File: {0}", VideoLocalID);

            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

            try
            {
                var repVids = new VideoLocalRepository();
                vlocal = repVids.GetByID(VideoLocalID);
                if (vlocal == null) return;

                //now that we have all the has info, we can get the AniDB Info
                ProcessFile_AniDB(vlocal);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_ProcessFile: {0} - {1}", VideoLocalID, ex.ToString());
            }

            // TODO update stats for group and series

            // TODO check for TvDB
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
                VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "VideoLocalID"));
                ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "ForceAniDB"));
            }

            return true;
        }

        private void ProcessFile_AniDB(VideoLocal vidLocal)
        {
            logger.Trace("Checking for AniDB_File record for: {0} --- {1}", vidLocal.Hash, vidLocal.FilePath);
            // check if we already have this AniDB_File info in the database

            var repAniFile = new AniDB_FileRepository();
            var repAniEps = new AniDB_EpisodeRepository();
            var repAniAnime = new AniDB_AnimeRepository();
            var repSeries = new AnimeSeriesRepository();
            var repVidLocals = new VideoLocalRepository();
            var repEps = new AnimeEpisodeRepository();
            var repXrefFE = new CrossRef_File_EpisodeRepository();

            AniDB_File aniFile = null;

            if (!ForceAniDB)
            {
                aniFile = repAniFile.GetByHashAndFileSize(vidLocal.Hash, vlocal.FileSize);

                if (aniFile == null)
                    logger.Trace("AniDB_File record not found");
            }

            var animeID = 0;

            if (aniFile == null)
            {
                // get info from AniDB
                logger.Debug("Getting AniDB_File record from AniDB....");
                var fileInfo = JMMService.AnidbProcessor.GetFileInfo(vidLocal);
                if (fileInfo != null)
                {
                    // check if we already have a record
                    aniFile = repAniFile.GetByHashAndFileSize(vidLocal.Hash, vlocal.FileSize);

                    if (aniFile == null)
                        aniFile = new AniDB_File();

                    aniFile.Populate(fileInfo);

                    //overwrite with local file name
                    var localFileName = Path.GetFileName(vidLocal.FilePath);
                    aniFile.FileName = localFileName;

                    repAniFile.Save(aniFile, false);
                    aniFile.CreateLanguages();
                    aniFile.CreateCrossEpisodes(localFileName);

                    if (!string.IsNullOrEmpty(fileInfo.OtherEpisodesRAW))
                    {
                        var epIDs = fileInfo.OtherEpisodesRAW.Split(',');
                        foreach (var epid in epIDs)
                        {
                            var id = 0;
                            if (int.TryParse(epid, out id))
                            {
                                var cmdEp = new CommandRequest_GetEpisode(id);
                                cmdEp.Save();
                            }
                        }
                    }

                    animeID = aniFile.AnimeID;
                }
            }

            var missingEpisodes = false;

            // if we still haven't got the AniDB_File Info we try the web cache or local records
            if (aniFile == null)
            {
                // check if we have any records from previous imports
                var crossRefs = repXrefFE.GetByHash(vidLocal.Hash);
                if (crossRefs == null || crossRefs.Count == 0)
                {
                    // lets see if we can find the episode/anime info from the web cache
                    if (ServerSettings.WebCache_XRefFileEpisode_Get)
                    {
                        var xrefs = AzureWebAPI.Get_CrossRefFileEpisode(vidLocal);

                        crossRefs = new List<CrossRef_File_Episode>();
                        if (xrefs == null || xrefs.Count == 0)
                        {
                            logger.Debug(
                                "Cannot find AniDB_File record or get cross ref from web cache record so exiting: {0}",
                                vidLocal.ED2KHash);
                            return;
                        }
                        foreach (var xref in xrefs)
                        {
                            var xrefEnt = new CrossRef_File_Episode();
                            xrefEnt.Hash = vidLocal.ED2KHash;
                            xrefEnt.FileName = Path.GetFileName(vidLocal.FullServerPath);
                            xrefEnt.FileSize = vidLocal.FileSize;
                            xrefEnt.CrossRefSource = (int)CrossRefSource.WebCache;
                            xrefEnt.AnimeID = xref.AnimeID;
                            xrefEnt.EpisodeID = xref.EpisodeID;
                            xrefEnt.Percentage = xref.Percentage;
                            xrefEnt.EpisodeOrder = xref.EpisodeOrder;

                            var duplicate = false;

                            foreach (var xrefcheck in crossRefs)
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
                                repXrefFE.Save(xrefEnt);
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
                foreach (var xref in crossRefs)
                {
                    animeID = xref.AnimeID;

                    var ep = repAniEps.GetByEpisodeID(xref.EpisodeID);
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
                    foreach (var xref in aniFile.EpisodeCrossRefs)
                    {
                        var ep = repAniEps.GetByEpisodeID(xref.EpisodeID);
                        if (ep == null)
                            missingEpisodes = true;

                        animeID = xref.AnimeID;
                    }
                }
            }

            // get from DB
            var anime = repAniAnime.GetByAnimeID(animeID);
            var animeRecentlyUpdated = false;

            if (anime != null)
            {
                var ts = DateTime.Now - anime.DateTimeUpdated;
                if (ts.TotalHours < 4) animeRecentlyUpdated = true;
            }

            // even if we are missing episode info, don't get data  more than once every 4 hours
            // this is to prevent banning
            if (missingEpisodes && !animeRecentlyUpdated)
            {
                logger.Debug("Getting Anime record from AniDB....");
                anime = JMMService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true, ServerSettings.AutoGroupSeries);
            }

            // create the group/series/episode records if needed
            AnimeSeries ser = null;
            if (anime != null)
            {
                logger.Debug("Creating groups, series and episodes....");
                // check if there is an AnimeSeries Record associated with this AnimeID
                ser = repSeries.GetByAnimeID(animeID);
                if (ser == null)
                {
                    // create a new AnimeSeries record
                    ser = anime.CreateAnimeSeriesAndGroup();
                }


                ser.CreateAnimeEpisodes();

                // check if we have any group status data for this associated anime
                // if not we will download it now
                var repStatus = new AniDB_GroupStatusRepository();
                if (repStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                {
                    var cmdStatus = new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false);
                    cmdStatus.Save();
                }

                // update stats
                ser.EpisodeAddedDate = DateTime.Now;
                repSeries.Save(ser);

                var repGroups = new AnimeGroupRepository();
                foreach (var grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    repGroups.Save(grp);
                }
            }

            vidLocal.RenameIfRequired();
            vidLocal.MoveFileIfRequired();


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
                var cmd = new CommandRequest_AddFileToMyList(vidLocal.ED2KHash);
                cmd.Save();
            }
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_ProcessFile_{0}", VideoLocalID);
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