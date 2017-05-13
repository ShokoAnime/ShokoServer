using System;
using System.Xml;
using Shoko.Models.Azure;
using Shoko.Models.Queue;
using Shoko.Server.Repositories.Direct;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBTvDB : CommandRequestImplementation, ICommandRequest
    {
        public int CrossRef_AniDB_TvDBID { get; set; }

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
                    queueState = QueueStateEnum.WebCacheSendXRefAniDBTvDB,
                    extraParams = new string[] {CrossRef_AniDB_TvDBID.ToString()}
                };
            }
        }

        public CommandRequest_WebCacheSendXRefAniDBTvDB()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBTvDB(int xrefID)
        {
            this.CrossRef_AniDB_TvDBID = xrefID;
            this.CommandType = (int) CommandRequestType.WebCache_SendXRefAniDBTvDB;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            try
            {
                //if (string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey)) return;

                CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByID(CrossRef_AniDB_TvDBID);
                if (xref == null) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error processing CommandRequest_WebCacheSendXRefAniDBTvDB: {0}" + ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBTvDB{0}", CrossRef_AniDB_TvDBID);
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
                this.CrossRef_AniDB_TvDBID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTvDB",
                        "CrossRef_AniDB_TvDBID"));
            }

            return true;
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