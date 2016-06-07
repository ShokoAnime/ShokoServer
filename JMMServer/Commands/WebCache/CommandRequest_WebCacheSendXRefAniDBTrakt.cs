using System;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Providers.Azure;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    public class CommandRequest_WebCacheSendXRefAniDBTrakt : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_WebCacheSendXRefAniDBTrakt()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBTrakt(int xrefID)
        {
            CrossRef_AniDB_TraktID = xrefID;
            CommandType = (int)CommandRequestType.WebCache_SendXRefAniDBTrakt;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int CrossRef_AniDB_TraktID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                return string.Format("Sending cross ref for Anidb to Trakt from web cache: {0}", CrossRef_AniDB_TraktID);
            }
        }

        public override void ProcessCommand()
        {
            try
            {
                var repCrossRef = new CrossRef_AniDB_TraktV2Repository();
                var xref = repCrossRef.GetByID(CrossRef_AniDB_TraktID);
                if (xref == null) return;

                var repShow = new Trakt_ShowRepository();
                var tvShow = repShow.GetByTraktSlug(xref.TraktID);
                if (tvShow == null) return;

                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                var showName = "";
                if (tvShow != null) showName = tvShow.Title;

                AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.ErrorException("Error processing CommandRequest_WebCacheSendXRefAniDBTrakt: {0}" + ex, ex);
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
                CrossRef_AniDB_TraktID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTrakt",
                        "CrossRef_AniDB_TraktID"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_WebCacheSendXRefAniDBTrakt{0}", CrossRef_AniDB_TraktID);
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