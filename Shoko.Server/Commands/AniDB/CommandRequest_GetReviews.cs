using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_GetReviews : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

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
                    queueState = QueueStateEnum.GetReviewInfo,
                    extraParams = new string[] {AnimeID.ToString()}
                };
            }
        }

        public CommandRequest_GetReviews()
        {
        }

        public CommandRequest_GetReviews(int animeid, bool forced)
        {
            this.AnimeID = animeid;
            this.ForceRefresh = forced;
            this.CommandType = (int) CommandRequestType.AniDB_GetReviews;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReviews: {0}", AnimeID);

            try
            {
                return;

                // we will always assume that an anime was downloaded via http first
                //Removed code as we have depreciated this effectively.
                /*SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);

                if (anime != null)
                {
                    // reviews count will be 0 when the anime is only downloaded via HTTP
                    if (ForceRefresh || anime.AnimeReviews.Count == 0)
                        anime = ShokoService.AnidbProcessor.GetAnimeInfoUDP(AnimeID, true);

                    foreach (AniDB_Anime_Review animeRev in anime.AnimeReviews)
                    {
                        ShokoService.AnidbProcessor.GetReviewUDP(animeRev.ReviewID);
                    }
                }*/
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetReviews: {0} - {1}", AnimeID, ex.ToString());
                return;
            }
        }

        public override void GenerateCommandID()
        {
            this.CommandID = string.Format("CommandRequest_GetReviews_{0}", this.AnimeID);
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
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "AnimeID"));
                this.ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "ForceRefresh"));
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