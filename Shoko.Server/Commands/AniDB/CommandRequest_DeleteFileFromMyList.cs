using System;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_DeleteFileUDP)]
    public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation
    {
        public string Hash { get; set; }
        public long FileSize { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.AniDB_MyListDelete,
            extraParams = new[] {Hash}
        };

        public CommandRequest_DeleteFileFromMyList()
        {
        }

        public CommandRequest_DeleteFileFromMyList(string hash, long fileSize)
        {
            Hash = hash;
            FileSize = fileSize;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand(IServiceProvider serviceProvider)
        {
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();
            // there will be a road bump the first time we start up, as some people may have requests with MyListID. I don't care. It'll get there.
            logger.Info("Processing CommandRequest_DeleteFileFromMyList: Hash: {Hash}, FileSize: {Size}", Hash, FileSize);

            try
            {
                UDPBaseRequest<Void> request;
                switch (ServerSettings.Instance.AniDb.MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        request = new RequestRemoveFile { Hash = Hash, Size = FileSize };
                        logger.Info("Deleting file from list: Hash: {Hash}", Hash);
                        request.Execute(handler);
                        break;

                    case AniDBFileDeleteType.MarkDeleted:
                        request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Deleted };
                        logger.Info("Marking file as deleted from list: Hash: {Hash}", Hash);
                        request.Execute(handler);
                        break;

                    case AniDBFileDeleteType.MarkUnknown:
                        request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Deleted };
                        logger.Info("Marking file as unknown: Hash: {Hash}", Hash);
                        request.Execute(handler);
                        break;

                    case AniDBFileDeleteType.DeleteLocalOnly:
                        logger.Info("Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", Hash);
                        break;

                    case AniDBFileDeleteType.MarkExternalStorage:
                        request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Remote };
                        logger.Info("Moving file to external storage: Hash: {Hash}", Hash);
                        request.Execute(handler);
                        break;
                    case AniDBFileDeleteType.MarkDisk:
                        request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Disk };
                        logger.Info("Moving file to disk (cd/dvd/bd): Hash: {Hash}", Hash);
                        request.Execute(handler);
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_AddFileToMyList: Hash: {Hash} - {Exception}", Hash, ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_DeleteFileFromMyList_{Hash}";
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "MyListID"), out var mylistID))
                {
                    var vid = RepoFactory.VideoLocal.GetByMyListID(mylistID);
                    if (vid != null)
                    {
                        Hash = vid.Hash;
                        FileSize = vid.FileSize;
                    }
                }
                else
                {
                    // populate the fields
                    Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "Hash");
                    FileSize = long.Parse(
                        TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", "FileSize")
                    );
                }
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