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
            if (FileID > 0)
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: Hash: {0}", Hash);
            else
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: FileID: {0}", FileID);

            try
            {
                switch (ServerSettings.AniDB_MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(FileID);
                            logger.Info("Deleting file from list: FileID: {0}", FileID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);
                            logger.Info("Deleting file from list: Hash: {0}", Hash);
                        }
                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(FileID);
                            logger.Info("Marking file as deleted from list: FileID: {0}", FileID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(Hash, FileSize);
                            logger.Info("Marking file as deleted from list: Hash: {0}", Hash);
                        }
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsUnknown(FileID);
                            logger.Info("Marking file as unknown: FileID: {0}", FileID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.MarkFileAsUnknown(Hash, FileSize);
                            logger.Info("Marking file as unknown: Hash: {0}", Hash);
                        }

                        break;

                    case AniDBFileDeleteType.DeleteLocalOnly:
                        if (FileID > 0)
                            logger.Info(
                                "Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {0}",
                                Hash);
                        else
                            logger.Info(
                                "Keeping physical file and AniDB MyList entry, deleting from local DB: FileID: {0}",
                                FileID);
                        break;

                    case AniDBFileDeleteType.MarkExternalStorage:
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsExternalStorage(FileID);
                            logger.Info("Moving file to external storage: FileID: {0}", FileID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.MarkFileAsExternalStorage(Hash, FileSize);
                            logger.Info("Moving file to external storage: Hash: {0}", Hash);
                        }
                        break;
                    case AniDBFileDeleteType.MarkDisk:
                        if (FileID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsOnDisk(FileID);
                            logger.Info("Moving file to external storage: FileID: {0}", FileID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.MarkFileAsOnDisk(Hash, FileSize);
                            logger.Info("Moving file to external storage: Hash: {0}", Hash);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(Hash))
                    logger.Error("Error processing CommandRequest_AddFileToMyList: Hash: {0} - {1}", Hash, ex);
                else
                    logger.Error("Error processing CommandRequest_AddFileToMyList: FileID: {0} - {1}", Hash, ex);
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