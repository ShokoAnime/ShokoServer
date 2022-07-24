using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.Azure;
using Shoko.Server.Repositories;
using Shoko.Server.Server;

namespace Shoko.Server.Commands
{
    [Command(CommandRequestType.WebCache_SendXRefAniDBTrakt)]
    public class CommandRequest_WebCacheSendXRefAniDBTrakt : CommandRequestImplementation
    {
        public int CrossRef_AniDB_TraktID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority10;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.WebCacheSendXRefAniDBTrakt,
            extraParams = new[] {CrossRef_AniDB_TraktID.ToString()}
        };

        public CommandRequest_WebCacheSendXRefAniDBTrakt()
        {
        }

        public CommandRequest_WebCacheSendXRefAniDBTrakt(int xrefID)
        {
            CrossRef_AniDB_TraktID = xrefID;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            try
            {
                CrossRef_AniDB_TraktV2 xref = RepoFactory.CrossRef_AniDB_TraktV2.GetByID(CrossRef_AniDB_TraktID);
                if (xref == null) return;

                Trakt_Show tvShow = RepoFactory.Trakt_Show.GetByTraktSlug(xref.TraktID);
                if (tvShow == null) return;

                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(xref.AnimeID);
                if (anime == null) return;

                string showName = string.Empty;
                if (tvShow != null) showName = tvShow.Title;

                AzureWebAPI.Send_CrossRefAniDBTrakt(xref, anime.MainTitle);
            }
            catch (Exception ex)
            {
                logger.Error(ex,
                    "Error processing CommandRequest_WebCacheSendXRefAniDBTrakt: {0}" + ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_WebCacheSendXRefAniDBTrakt{CrossRef_AniDB_TraktID}";
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
                CrossRef_AniDB_TraktID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_WebCacheSendXRefAniDBTrakt",
                        "CrossRef_AniDB_TraktID"));
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