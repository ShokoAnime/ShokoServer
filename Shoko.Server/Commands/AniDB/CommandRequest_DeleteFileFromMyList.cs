using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_DeleteFileUDP)]
    public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation
    {
        public string Hash { get; set; }
        public long FileSize { get; set; }
        public int FileID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_MyListDelete,
            extraParams = new[] {Hash, FileID.ToString()}
        };

        public CommandRequest_DeleteFileFromMyList()
        {
        }

        public CommandRequest_DeleteFileFromMyList(string hash, long fileSize)
        {
            Hash = hash;
            FileSize = fileSize;
            FileID = -1;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public CommandRequest_DeleteFileFromMyList(int fileID)
        {
            Hash = string.Empty;
            FileSize = 0;
            FileID = fileID;
            Priority = (int) DefaultPriority;

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
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(Hash, FileSize);
                            logger.Info("Marking file as deleted from list: {0}_{1}", Hash, FileID);
                        }
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (FileID > 0)
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
                        if (FileID > 0)
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
                    ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_DeleteFileFromMyList_{Hash}_{FileID}";
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "Hash");
                FileSize = long.Parse(
                    TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileSize"));
                FileID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileID"));
            }

            if (Hash.Trim().Length > 0)
                return true;
            return false;
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