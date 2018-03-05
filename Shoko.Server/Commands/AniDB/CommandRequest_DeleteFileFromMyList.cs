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
        public int MyListID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_MyListDelete,
            extraParams = new[] {Hash, MyListID.ToString()}
        };

        public CommandRequest_DeleteFileFromMyList()
        {
        }

        public CommandRequest_DeleteFileFromMyList(int myListID)
        {
            Hash = string.Empty;
            FileSize = 0;
            MyListID = myListID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            if (MyListID > 0)
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: Hash: {0}", Hash);
            else
                logger.Info("Processing CommandRequest_DeleteFileFromMyList: MyListID: {0}", MyListID);

            try
            {
                switch (ServerSettings.AniDB_MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(MyListID);
                            logger.Info("Deleting file from list: MyListID: {0}", MyListID);
                        }
                        else
                        {
                            ShokoService.AnidbProcessor.DeleteFileFromMyList(Hash, FileSize);
                            logger.Info("Deleting file from list: Hash: {0}", Hash);
                        }
                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsDeleted(MyListID);
                            logger.Info("Marking file as deleted from list: MyListID: {0}", MyListID);
                        }
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsUnknown(MyListID);
                            logger.Info("Marking file as unknown: MyListID: {0}", MyListID);
                        }

                        break;

                    case AniDBFileDeleteType.DeleteLocalOnly:
                        if (MyListID > 0)
                            logger.Info(
                                "Keeping physical file and AniDB MyList entry, deleting from local DB: MyListID: {0}",
                                MyListID);
                        else
                            logger.Info(
                                "Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {0}",
                                Hash);
                        break;

                    case AniDBFileDeleteType.MarkExternalStorage:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsRemote(MyListID);
                            logger.Info("Moving file to external storage: MyListID: {0}", MyListID);
                        }
                        break;
                    case AniDBFileDeleteType.MarkDisk:
                        if (MyListID > 0)
                        {
                            ShokoService.AnidbProcessor.MarkFileAsOnDisk(MyListID);
                            logger.Info("Moving file to external storage: MyListID: {0}", MyListID);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrEmpty(Hash))
                    logger.Error("Error processing CommandRequest_AddFileToMyList: Hash: {0} - {1}", Hash, ex);
                else
                    logger.Error("Error processing CommandRequest_AddFileToMyList: MyListID: {0} - {1}", MyListID, ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_DeleteFileFromMyList_{Hash}_{MyListID}";
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
                MyListID = int.Parse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "MyListID"));
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