using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.AniDB_GetReviews)]
    public class CommandRequest_GetReviews : CommandRequestImplementation
    {
        public virtual int AnimeID { get; set; }
        public virtual bool ForceRefresh { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority5;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GetReviewInfo,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_GetReviews()
        {
        }

        public CommandRequest_GetReviews(int animeid, bool forced)
        {
            AnimeID = animeid;
            ForceRefresh = forced;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReviews: {0}", AnimeID);

            try
            {
                // we will always assume that an anime was downloaded via http first
                //Removed code as we have depreciated this effectively.
                /*SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(AnimeID);

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
                logger.Error("Error processing CommandRequest_GetReviews: {0} - {1}", AnimeID, ex);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_GetReviews_{AnimeID}";
        }

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "AnimeID"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "ForceRefresh"));
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