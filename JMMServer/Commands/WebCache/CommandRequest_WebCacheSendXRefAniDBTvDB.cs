using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using Shoko.Models.Azure;
using JMMServer.Repositories;
using JMMServer.Repositories.Direct;
using Shoko.Models.Server;

namespace JMMServer.Commands
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
                return new QueueStateStruct() { queueState = QueueStateEnum.WebCacheSendXRefAniDBTvDB, extraParams = new string[] { CrossRef_AniDB_TvDBID.ToString() } };
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

                SVR_CrossRef_AniDB_TvDBV2 xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByID(CrossRef_AniDB_TvDBID);
                if (xref == null) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBTvDB: {0}" + ex.ToString(),
                    ex);
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

            CommandRequest cq = new CommandRequest();
            cq.CommandID = this.CommandID;
            cq.CommandType = this.CommandType;
            cq.Priority = this.Priority;
            cq.CommandDetails = this.ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}