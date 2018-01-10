using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBTvDB : CommandRequest
    {
        public virtual int CrossRef_AniDB_TvDBID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefAniDBTvDB,
            extraParams = new[] {CrossRef_AniDB_TvDBID.ToString()}
        };

        public CommandRequest_WebCacheSendXRefAniDBTvDB()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBTvDB(int xrefID)
        {
            CrossRef_AniDB_TvDBID = xrefID;
            CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBTvDB;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                //if (string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey)) return;

                CrossRef_AniDB_TvDBV2 xref = Repo.CrossRef_AniDB_TvDBV2.GetByID(CrossRef_AniDB_TvDBID);
                if (xref == null) return;

                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_WebCacheSendXRefAniDBTvDB: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDBTvDB{CrossRef_AniDB_TvDBID}";
        }

        public override bool InitFromDB(CommandRequest cq)
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                CrossRef_AniDB_TvDBID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTvDB",
                        "CrossRef_AniDB_TvDBID"));
            }

            return true;
        }
    }
}