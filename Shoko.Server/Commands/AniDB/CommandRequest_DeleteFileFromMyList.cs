using System;
using System.Xml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shoko.Commons.Queue;
using Shoko.Models.Enums;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Providers.AniDB;
using Shoko.Server.Providers.AniDB.Interfaces;
using Shoko.Server.Providers.AniDB.UDP.Generic;
using Shoko.Server.Providers.AniDB.UDP.User;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;
using EpisodeType = Shoko.Models.Enums.EpisodeType;
using Void = Shoko.Server.Providers.AniDB.UDP.Generic.Void;

namespace Shoko.Server.Commands.AniDB
{
    [Serializable]
    [Command(CommandRequestType.AniDB_DeleteFileUDP)]
    public class CommandRequest_DeleteFileFromMyList : CommandRequestImplementation
    {
        public string Hash { get; set; }
        public long FileSize { get; set; }
        public EpisodeType EpisodeType { get; set; }
        public int EpisodeNumber { get; set; }
        public int AnimeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            message = string.IsNullOrEmpty(Hash) ? "Deleting file from MyList: {0}, Episode: {1} {2}" : "Deleting file from MyList: {0}",
            queueState = QueueStateEnum.AniDB_MyListDelete,
            extraParams = string.IsNullOrEmpty(Hash) ? new[] { AnimeID.ToString(), EpisodeType.ToString(), EpisodeNumber.ToString() } : new[] { Hash },
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

        protected override void Process(IServiceProvider serviceProvider)
        {
            var handler = serviceProvider.GetRequiredService<IUDPConnectionHandler>();
            // there will be a road bump the first time we start up, as some people may have requests with MyListID. I don't care. It'll get there.
            Logger.LogInformation("Processing CommandRequest_DeleteFileFromMyList: Hash: {Hash}, FileSize: {Size}", Hash, FileSize);

            try
            {
                UDPRequest<Void> request;
                switch (ServerSettings.Instance.AniDb.MyList_DeleteType)
                {
                    case AniDBFileDeleteType.Delete:
                        if (string.IsNullOrEmpty(Hash))
                        {
                            request = new RequestRemoveEpisode { AnimeID = AnimeID, EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType, EpisodeNumber = EpisodeNumber };
                            Logger.LogInformation("Deleting Episode from MyList: AnimeID: {AnimeID} {EpisodeType} {Number}", AnimeID, EpisodeType, EpisodeNumber);
                            request.Execute(handler);
                        }
                        else
                        {
                            request = new RequestRemoveFile { Hash = Hash, Size = FileSize };
                            Logger.LogInformation("Deleting file from MyList: Hash: {Hash}", Hash);
                            request.Execute(handler);                            
                        }

                        break;
                    case AniDBFileDeleteType.MarkDeleted:
                        if (string.IsNullOrEmpty(Hash))
                        {
                            request = new RequestUpdateEpisode { AnimeID = AnimeID, EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType, EpisodeNumber = EpisodeNumber, State = MyList_State.Deleted };
                            Logger.LogInformation("Marking Episode as deleted in MyList: AnimeID: {AnimeID} {EpisodeType} {Number}", AnimeID, EpisodeType, EpisodeNumber);
                            request.Execute(handler);
                        }
                        else
                        {
                            request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Deleted };
                            Logger.LogInformation("Marking file as deleted in MyList: Hash: {Hash}", Hash);
                            request.Execute(handler);
                        }

                        break;
                    case AniDBFileDeleteType.MarkUnknown:
                        if (string.IsNullOrEmpty(Hash))
                        {
                            request = new RequestUpdateEpisode { AnimeID = AnimeID, EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType, EpisodeNumber = EpisodeNumber, State = MyList_State.Unknown };
                            Logger.LogInformation("Marking Episode as unknown in MyList: AnimeID: {AnimeID} {EpisodeType} {Number}", AnimeID, EpisodeType, EpisodeNumber);
                            request.Execute(handler);
                        }
                        else
                        {
                            request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Unknown };
                            Logger.LogInformation("Marking file as unknown in MyList: Hash: {Hash}", Hash);
                            request.Execute(handler);
                        }

                        break;
                    case AniDBFileDeleteType.DeleteLocalOnly:
                        Logger.LogInformation("Keeping physical file and AniDB MyList entry, deleting from local DB: Hash: {Hash}", Hash);
                        break;
                    case AniDBFileDeleteType.MarkExternalStorage:
                        if (string.IsNullOrEmpty(Hash))
                        {
                            request = new RequestUpdateEpisode { AnimeID = AnimeID, EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType, EpisodeNumber = EpisodeNumber, State = MyList_State.Remote };
                            Logger.LogInformation("Marking Episode as remote in MyList: AnimeID: {AnimeID} {EpisodeType} {Number}", AnimeID, EpisodeType, EpisodeNumber);
                            request.Execute(handler);
                        }
                        else
                        {
                            request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Remote };
                            Logger.LogInformation("Marking file as remote in MyList: Hash: {Hash}", Hash);
                            request.Execute(handler);
                        }
                        break;
                    case AniDBFileDeleteType.MarkDisk:
                        if (string.IsNullOrEmpty(Hash))
                        {
                            request = new RequestUpdateEpisode { AnimeID = AnimeID, EpisodeType = (Providers.AniDB.EpisodeType)(int)EpisodeType, EpisodeNumber = EpisodeNumber, State = MyList_State.Disk };
                            Logger.LogInformation("Marking Episode as Disk in MyList: AnimeID: {AnimeID} {EpisodeType} {Number}", AnimeID, EpisodeType, EpisodeNumber);
                            request.Execute(handler);
                        }
                        else
                        {
                            request = new RequestUpdateFile { Hash = Hash, Size = FileSize, State = MyList_State.Disk };
                            Logger.LogInformation("Marking file as Disk in MyList: Hash: {Hash}", Hash);
                            request.Execute(handler);
                        }
                        break;
                }
            }
            catch (AniDBBannedException ex)
            {
                Logger.LogError(ex, "Error processing {Type}: Hash: {Hash} - {Exception}", GetType().Name, Hash, ex);
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
                    Hash = TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(Hash));
                    FileSize = long.Parse(
                        TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(FileSize))
                    );

                    if (Enum.TryParse<EpisodeType>(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(EpisodeType)), out var episodeType))
                        EpisodeType = episodeType;
                    if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(EpisodeNumber)), out var epNum))
                        EpisodeNumber = epNum;
                    if (int.TryParse(TryGetProperty(docCreator, "CommandRequest_DeleteFileFromMyList", nameof(AnimeID)), out var animeID))
                        AnimeID = animeID;
                }
            }

            if (Hash.Trim().Length > 0 || AnimeID > 0)
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