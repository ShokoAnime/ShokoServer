using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBTvDB : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheSendXRefAniDBTvDB()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBTvDB(int xrefID)
        {
            CrossRef_AniDB_TvDBID = xrefID;
            CommandType = (int)CommandRequestType.WebCache_SendXRefAniDBTvDB;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int CrossRef_AniDB_TvDBID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Sending cross ref for Anidb to TvDB from web cache: {0}", CrossRef_AniDB_TvDBID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                //if (string.IsNullOrEmpty(ServerSettings.WebCacheAuthKey)) return;

                var repCrossRef = new CrossRef_AniDB_TvDBV2Repository();
                var xref = repCrossRef.GetByID(CrossRef_AniDB_TvDBID);
                if (xref == null) return;

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                AzureWebAPI.Send_CrossRefAniDBTvDB(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBTvDB: {0}" + ex, ex);
            }
        }

        public override bool LoadFromDBCommand(CommandRequest cq)
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
                var docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                CrossRef_AniDB_TvDBID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTvDB",
                        "CrossRef_AniDB_TvDBID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBTvDB{0}", CrossRef_AniDB_TvDBID);
        }

        public override CommandRequest ToDatabaseObject()
        {
            GenerateCommandID();

            var cq = new CommandRequest();
            cq.CommandID = CommandID;
            cq.CommandType = CommandType;
            cq.Priority = Priority;
            cq.CommandDetails = ToXML();
            cq.DateTimeUpdated = DateTime.Now;

            return cq;
        }
    }
}