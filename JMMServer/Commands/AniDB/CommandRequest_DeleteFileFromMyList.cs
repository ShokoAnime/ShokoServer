using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using AniDBAPI;
using JMMServer.Entities;
using JMMServer.Properties;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_DeleteFileFromMyList()
        {
        }

        public CommandRequest_DeleteFileFromMyList(string hash, long fileSize)
        {
            Hash = hash;
            FileSize = fileSize;
            FileID = -1;
            CommandType = (int)CommandRequestType.AniDB_DeleteFileUDP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequest_DeleteFileFromMyList(int fileID)
        {
            Hash = "";
            FileSize = 0;
            FileID = fileID;
            CommandType = (int)CommandRequestType.AniDB_DeleteFileUDP;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public string Hash { get; set; }
        public long FileSize { get; set; }
        public int FileID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.AniDB_MyListDelete, Hash, FileID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_DeleteFileFromMyList: {0}_{1}", Hash, FileID);

            try
            {
                switch (ServerSettings.AniDB_MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        if (FileID > 0)
                            JMMService.AnidbProcessor.DeleteFileFromMyList(FileID);
                        else
                            JMMService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);

                        logger.Info("Deleting file from list: {0}_{1}", Hash, FileID);
                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        if (FileID < 0)
                        {
                            JMMService.AnidbProcessor.MarkFileAsDeleted(Hash, FileSize);
                            logger.Info("Marking file as deleted from list: {0}_{1}", Hash, FileID);
                        }
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (FileID < 0)
                        {
                            JMMService.AnidbProcessor.MarkFileAsUnknown(Hash, FileSize);
                            logger.Info("Marking file as unknown: {0}_{1}", Hash, FileID);
                        }
                        break;

                    case AniDBFileDeleteType.DeleteLocalOnly:
                        logger.Info("Keeping physical file and AniDB MyList entry, deleting from local DB: {0}_{1}",
                            Hash, FileID);
                        break;

                    default:
                        if (FileID < 0)
                        {
                            JMMService.AnidbProcessor.MarkFileAsExternalStorage(Hash, FileSize);
                            logger.Info("Moving file to external storage: {0}_{1}", Hash, FileID);
                        }
                        break;
                }


                if (ServerSettings.AniDB_MyList_DeleteType == AniDBFileDeleteType.Delete ||
                    ServerSettings.AniDB_MyList_DeleteType == AniDBFileDeleteType.MarkDeleted)
                {
                    /*VideoLocalRepository repVids = new VideoLocalRepository();
                    VideoLocal vid = repVids.GetByHash(this.Hash);

                    // lets also try adding to the users trakt collecion
                    if (ServerSettings.Trakt_IsEnabled && !string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken))
                    {
                        AnimeEpisodeRepository repEpisodes = new AnimeEpisodeRepository();
                        List<AnimeEpisode> animeEpisodes = vid.GetAnimeEpisodes();

                        foreach (AnimeEpisode aep in animeEpisodes)
                        {
                            CommandRequest_TraktCollectionEpisode cmdSyncTrakt = new CommandRequest_TraktCollectionEpisode(aep.AnimeEpisodeID, TraktSyncAction.Remove);
                            cmdSyncTrakt.Save();
                        }

                    }*/

                    // By the time we get to this point, the VideoLocal records would have been deleted
                    // So we can't get the episode records to do this on an ep by ep basis
                    // lets also try adding to the users trakt collecion by sync'ing the series
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_AddFileToMyList: {0}_{1} - {2}", Hash, FileID,
                    ex.ToString());
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
                Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "Hash");
                FileSize = long.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileSize"));
                FileID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileID"));
            }

            if (Hash.Trim().Length > 0)
                return true;
            return false;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_DeleteFileFromMyList_{0}_{1}", Hash, FileID);
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