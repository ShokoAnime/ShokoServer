using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Extensions;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_SendXRefAniDB)]
    public class CommandRequest_WebCacheSendXRef : CommandRequestImplementation
    {
        public int CrossRef_AniDB { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefAniDBTvDB,
            extraParams = new[] {CrossRef_AniDB.ToString()}
        };

        public CommandRequest_WebCacheSendXRef()
        {
        }

        public CommandRequest_WebCacheSendXRef(int xrefID)
        {
            CrossRef_AniDB = xrefID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                //if (string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey)) return;

                CrossRef_AniDB xref = RepoFactory.CrossRef_AniDB.GetByID(CrossRef_AniDB);
                if (xref == null) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AniDBID);
                if (anime == null) return;

                AzureWebAPI.Send_CrossRefAniDB(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_WebCacheSendXRefAniDB: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDB{CrossRef_AniDB}";
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
                CrossRef_AniDB =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDB",
                        "CrossRef_AniDB"));
            }

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
