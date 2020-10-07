using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using AniDBAPI;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Azure;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Databases;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.ProcessFile)]
    public class CommandRequest_ProcessFile : CommandRequestImplementation
    {
        public int VideoLocalID { get; set; }
        public bool ForceAniDB { get; set; }
        
        public bool SkipMyList { get; set; }

        private SVR_VideoLocal vlocal;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority3;

        public override QueueStateStruct PrettyDescription
        {
            get
            {
                if (vlocal != null)
                    return new QueueStateStruct
                    {
                        queueState = QueueStateEnum.FileInfo,
                        extraParams = new[] {vlocal.FileName}
                    };
                return new QueueStateStruct
                {
                    queueState = QueueStateEnum.FileInfo,
                    extraParams = new[] {VideoLocalID.ToString()}
                };
            }
        }

        public CommandRequest_ProcessFile()
        {
        }

        public CommandRequest_ProcessFile(int vidLocalID, bool forceAniDB, bool skipMyList = false)
        {
            VideoLocalID = vidLocalID;
            ForceAniDB = forceAniDB;
            Priority = (int) DefaultPriority;
            SkipMyList = skipMyList;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Trace($"Processing File: {VideoLocalID}");

            try
            {
                if (vlocal == null) vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);
                if (vlocal == null) return;

                //now that we have all the has info, we can get the AniDB Info
                ProcessFile_AniDB(vlocal);
            }
            catch (Exception ex)
            {
                logger.Error($"Error processing CommandRequest_ProcessFile: {VideoLocalID} - {ex}");
            }
        }

        private void ProcessFile_AniDB(SVR_VideoLocal vidLocal)
        {
            logger.Trace($"Checking for AniDB_File record for: {vidLocal.Hash} --- {vidLocal.FileName}");
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

                // If cross refs were wiped, but the AniDB_File was not, we unfortunately need to requery the info
                List<CrossRef_File_Episode> crossRefs = RepoFactory.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
                if (crossRefs == null || crossRefs.Count == 0) aniFile = null;

                Dictionary<int, bool> animeIDs = new Dictionary<int, bool>();

                if (aniFile == null) aniFile = TryGetAniDBFileFromAniDB(vidLocal, animeIDs);

                // if we still haven't got the AniDB_File Info we try the web cache or local records
                if (aniFile == null)
                {
                    // check if we have any records from previous imports
                    crossRefs = RepoFactory.CrossRef_File_Episode.GetByHash(vidLocal.Hash);
                    if (crossRefs == null || crossRefs.Count == 0)
                    {
                        // lets see if we can find the episode/anime info from the web cache
                        if (TryGetCrossRefsFromWebCache(vidLocal, ref crossRefs)) return;
                    }

                    // we assume that all episodes belong to the same anime
                    foreach (CrossRef_File_Episode xref in crossRefs)
                    {
                        AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);
                        if (animeIDs.ContainsKey(xref.AnimeID)) animeIDs[xref.AnimeID] = ep == null;
                        else animeIDs.Add(xref.AnimeID, ep == null);
                    }
                }
                else
                {
                    // check if we have the episode info
                    // if we don't, we will need to re-download the anime info (which also has episode info)
                    if (aniFile.EpisodeCrossRefs.Count == 0)
                    {
                        aniFile.Episodes.Select(a => a.AnimeID).Distinct().ForEach(animeID =>
                        {
                            if (animeIDs.ContainsKey(animeID)) animeIDs[animeID] = true;
                            else animeIDs.Add(animeID, true);
                        });

                        // if we have the AniDB file, but no cross refs it means something has been broken
                        logger.Debug($"Could not find any cross ref records for: {vidLocal.ED2KHash}");
                    }
                    else
                    {
                        foreach (CrossRef_File_Episode xref in aniFile.EpisodeCrossRefs)
                        {
                            AniDB_Episode ep = RepoFactory.AniDB_Episode.GetByEpisodeID(xref.EpisodeID);

                            if (animeIDs.ContainsKey(xref.AnimeID)) animeIDs[xref.AnimeID] = ep == null;
                            else animeIDs.Add(xref.AnimeID, ep == null);
                        }
                    }
                }

                PopulateAnimeForFile(vidLocal, animeIDs);

                // We do this inside, as the info will not be available as needed otherwise
                List<SVR_VideoLocal> videoLocals =
                    aniFile?.EpisodeIDs?.SelectMany(a => RepoFactory.VideoLocal.GetByAniDBEpisodeID(a))
                        .Where(b => b != null)
                        .ToList();
                if (videoLocals != null)
                {
                    if (ServerSettings.Instance.Import.UseExistingFileWatchedStatus)
                    {
                        // Copy over watched states
                        foreach (var user in RepoFactory.JMMUser.GetAll())
                        {
                            var watchedVideo = videoLocals.FirstOrDefault(a =>
                                a?.GetUserRecord(user.JMMUserID)?.WatchedDate != null);
                            // No files that are watched
                            if (watchedVideo == null) continue;

                            var watchedRecord = watchedVideo.GetUserRecord(user.JMMUserID);
                            var userRecord = vidLocal.GetUserRecord(user.JMMUserID) ?? new VideoLocal_User
                            {
                                JMMUserID = user.JMMUserID,
                                VideoLocalID = vidLocal.VideoLocalID,
                            };

                            userRecord.WatchedDate = watchedRecord.WatchedDate;
                            userRecord.ResumePosition = watchedRecord.ResumePosition;

                            RepoFactory.VideoLocalUser.Save(userRecord);
                        }
                    }

                    // update stats for groups and series. The series are not saved until here, so it's absolutely necessary!!
                    animeIDs.Keys.ForEach(SVR_AniDB_Anime.UpdateStatsByAnimeID);

                    if (ServerSettings.Instance.FileQualityFilterEnabled)
                    {
                        videoLocals.Sort(FileQualityFilter.CompareTo);
                        List<SVR_VideoLocal> keep = videoLocals
                            .Take(FileQualityFilter.Settings.MaxNumberOfFilesToKeep)
                            .ToList();
                        foreach (SVR_VideoLocal vl2 in keep) videoLocals.Remove(vl2);
                        if (!FileQualityFilter.Settings.AllowDeletionOfImportedFiles &&
                            videoLocals.Contains(vidLocal)) videoLocals.Remove(vidLocal);
                        videoLocals = videoLocals.Where(a => !FileQualityFilter.CheckFileKeep(a)).ToList();

                        videoLocals.ForEach(a => a.Places.ForEach(b => b.RemoveAndDeleteFile()));
                    }
                }

                vidLocal.Places.ForEach(a => { a.RenameAndMoveAsRequired(); });

                // Add this file to the users list
                if (ServerSettings.Instance.AniDb.MyList_AddFiles && !SkipMyList && vidLocal.MyListID <= 0)
                {
                    new CommandRequest_AddFileToMyList(vidLocal.ED2KHash).Save();
                }
            }
        }

        private static void PopulateAnimeForFile(SVR_VideoLocal vidLocal, Dictionary<int, bool> animeIDs)
        {
            foreach (KeyValuePair<int, bool> kV in animeIDs)
            {
                int animeID = kV.Key;
                bool missingEpisodes = kV.Value;
                // get from DB
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(animeID);
                var update = RepoFactory.AniDB_AnimeUpdate.GetByAnimeID(animeID);
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
                    anime = ShokoService.AnidbProcessor.GetAnimeInfoHTTP(animeID, true,
                        ServerSettings.Instance.AutoGroupSeries ||
                        ServerSettings.Instance.AniDb.DownloadRelatedAnime);
                }

                // create the group/series/episode records if needed
                if (anime == null)
                {
                    logger.Warn($"Unable to create AniDB_Anime for file: {vidLocal.FileName}");
                    logger.Warn($"Queuing GET for AniDB_Anime: {animeID}");
                    CommandRequest_GetAnimeHTTP animeCommand = new CommandRequest_GetAnimeHTTP(animeID, true, true);
                    animeCommand.Save();
                    return;
                }

                logger.Debug("Creating groups, series and episodes....");
                // check if there is an AnimeSeries Record associated with this AnimeID
                var ser = RepoFactory.AnimeSeries.GetByAnimeID(animeID);

                if (ser == null)
                {
                    // We will put UpdatedAt in the CreateAnimeSeriesAndGroup method, to ensure it exists at first write
                    ser = anime.CreateAnimeSeriesAndGroup();
                    ser.CreateAnimeEpisodes();
                }
                else
                {
                    TimeSpan ts = DateTime.Now - ser.UpdatedAt;

                    // don't even check episodes if we've done it recently...
                    if (ts.TotalHours > 6)
                    {
                        if (ser.NeedsEpisodeUpdate())
                        {
                            logger.Info(
                                $"Series {anime.MainTitle} needs episodes regenerated (an episode was added or deleted from AniDB)");
                            ser.CreateAnimeEpisodes();
                            ser.UpdatedAt = DateTime.Now;
                        }
                    }
                }

                // check if we have any group status data for this associated anime
                // if not we will download it now
                if (RepoFactory.AniDB_GroupStatus.GetByAnimeID(anime.AnimeID).Count == 0)
                    new CommandRequest_GetReleaseGroupStatus(anime.AnimeID, false).Save();

                // Only save the date, we'll update GroupFilters and stats in one pass
                // don't bother saving the series here, it'll happen in SVR_AniDB_Anime.UpdateStatsByAnimeID()
                // just don't do anything that needs this changed data before then
                ser.EpisodeAddedDate = DateTime.Now;

                foreach (SVR_AnimeGroup grp in ser.AllGroupsAbove)
                {
                    grp.EpisodeAddedDate = DateTime.Now;
                    RepoFactory.AnimeGroup.Save(grp, false, false, false);
                }
            }
        }

        private SVR_AniDB_File TryGetAniDBFileFromAniDB(SVR_VideoLocal vidLocal, Dictionary<int, bool> animeIDs)
        {
            // check if we already have a record
            SVR_AniDB_File aniFile = RepoFactory.AniDB_File.GetByHashAndFileSize(vidLocal.Hash, vlocal.FileSize);

            if (aniFile == null) ForceAniDB = true;

            if (ForceAniDB)
            {
                if (!ShokoService.AnidbProcessor.IsUdpBanned)
                {
                    // get info from AniDB
                    logger.Debug("Getting AniDB_File record from AniDB....");
                    Raw_AniDB_File fileInfo = ShokoService.AnidbProcessor.GetFileInfo(vidLocal);
                    if (fileInfo != null)
                    {
                        aniFile ??= new SVR_AniDB_File();
                        SVR_AniDB_File.Populate(aniFile, fileInfo);
                    }
                }
                else
                {
                    CommandRequest_GetFile fileCommand = new CommandRequest_GetFile(vlocal.VideoLocalID, true);
                    fileCommand.Save();
                }
            }

            if (aniFile == null) return null;
            //overwrite with local file name
            string localFileName = vidLocal.GetBestVideoLocalPlace()?.FullServerPath;
            localFileName = !string.IsNullOrEmpty(localFileName)
                ? Path.GetFileName(localFileName)
                : vidLocal.FileName;
            aniFile.FileName = localFileName;

            RepoFactory.AniDB_File.Save(aniFile, false);
            aniFile.CreateLanguages();
            aniFile.CreateCrossEpisodes(localFileName);

            aniFile.Episodes.Select(a => a.AnimeID).Distinct().ForEach(animeID =>
            {
                if (animeIDs.ContainsKey(animeID)) animeIDs[animeID] = false;
                else animeIDs.Add(animeID, false);
            });

            return aniFile;
        }

        private static bool TryGetCrossRefsFromWebCache(SVR_VideoLocal vidLocal, ref List<CrossRef_File_Episode> crossRefs)
        {
            if (!ServerSettings.Instance.WebCache.Enabled || !ServerSettings.Instance.WebCache.XRefFileEpisode_Get)
            {
                logger.Debug($"Cannot get AniDB_File record so exiting: {vidLocal.ED2KHash}");
                return true;
            }

            List<Azure_CrossRef_File_Episode> xrefs = AzureWebAPI.Get_CrossRefFileEpisode(vidLocal);

            crossRefs = new List<CrossRef_File_Episode>();
            if (xrefs == null || xrefs.Count == 0)
            {
                logger.Debug(
                    $"Cannot find AniDB_File record or get cross ref from web cache record so exiting: {vidLocal.ED2KHash}");
                return true;
            }

            string fileName = vidLocal.GetBestVideoLocalPlace()?.FullServerPath;
            fileName = !string.IsNullOrEmpty(fileName) ? Path.GetFileName(fileName) : vidLocal.FileName;
            foreach (Azure_CrossRef_File_Episode xref in xrefs)
            {
                bool duplicate = crossRefs.Any(a =>
                    a.AnimeID == xref.AnimeID && a.EpisodeID == xref.EpisodeID && a.Hash == xref.Hash);

                if (duplicate) continue;

                CrossRef_File_Episode xref2 = new CrossRef_File_Episode
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
                crossRefs.Add(xref2);
                // in this case we need to save the cross refs manually as AniDB did not provide them
                // use a session to prevent updating stats
                using var session = DatabaseFactory.SessionFactory.OpenSession();
                using var trans = session.BeginTransaction();
                RepoFactory.CrossRef_File_Episode.SaveWithOpenTransaction(session, xref2);
                trans.Commit();
            }

            return false;
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_ProcessFile_{VideoLocalID}";
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
        {
            CommandID = cq.CommandID;
            CommandRequestID = cq.CommandRequestID;
            Priority = cq.Priority;
            CommandDetails = cq.CommandDetails;
            DateTimeUpdated = cq.DateTimeUpdated;

            // read xml to get parameters
            if (CommandDetails.Trim().Length <= 0) return true;
            XmlDocument docCreator = new XmlDocument();
            docCreator.LoadXml(CommandDetails);

            // populate the fields
            VideoLocalID = int.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "VideoLocalID"));
            ForceAniDB = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "ForceAniDB"));
            SkipMyList = bool.Parse(TryGetProperty(docCreator, "CommandRequest_ProcessFile", "SkipMyList"));
            vlocal = RepoFactory.VideoLocal.GetByID(VideoLocalID);

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
