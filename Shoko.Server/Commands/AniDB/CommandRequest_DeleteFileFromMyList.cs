using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation, ICommandRequest
    {
        public string Hash { get; set; }
        public long FileSize { get; set; }
        public int FileID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct()
                {
                    queueState = QueueStateEnum.AniDB_MyListDelete,
                    extraParams = new string[] {Hash, FileID.ToString()}
                };
            }
        }

        public CommandRequest_DeleteFileFromMyList()
        {
        }

        public CommandRequest_DeleteFileFromMyList(string hash, long fileSize)
        {
            this.Hash = hash;
            this.FileSize = fileSize;
            this.FileID = -1;
            this.CommandType = (int) CommandRequestType.AniDB_DeleteFileUDP;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequest_DeleteFileFromMyList(int fileID)
        {
            this.Hash = "";
            this.FileSize = 0;
            this.FileID = fileID;
            this.CommandType = (int) CommandRequestType.AniDB_DeleteFileUDP;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
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
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(FileID);
                        else
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);
                        logger.Info("Deleting file from list: {0}_{1}", Hash, FileID);
                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        if (FileID < 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(Hash, FileSize);
                            logger.Info("Marking file as deleted from list: {0}_{1}", Hash, FileID);
                        }
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (FileID < 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsUnknown(Hash, FileSize);
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
                            ShokoService.AnidbProcessor.MarkFileAsExternalStorage(Hash, FileSize);
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
                return;
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_DeleteFileFromMyList_{0}_{1}", Hash, FileID);
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
                this.Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "Hash");
                this.FileSize = long.Parse(
                    TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileSize"));
                this.FileID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileID"));
            }

            if (this.Hash.Trim().Length > 0)
                return true;
            else
                return false;
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