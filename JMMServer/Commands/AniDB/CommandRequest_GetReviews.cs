using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_GetReviews : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_GetReviews()
        {
        }

        public CommandRequest_GetReviews(int animeid, bool forced)
        {
            AnimeID = animeid;
            ForceRefresh = forced;
            CommandType = (int)CommandRequestType.AniDB_GetReviews;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeID { get; set; }
        public bool ForceRefresh { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_GetReviewInfo, AnimeID);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_GetReviews: {0}", AnimeID);

            try
            {
                return;

                // we will always assume that an anime was downloaded via http first
                var repAnime = new AniDB_AnimeRepository();
                var anime = repAnime.GetByAnimeID(AnimeID);

                if (anime != null)
                {
                    // reviews count will be 0 when the anime is only downloaded via HTTP
                    if (ForceRefresh || anime.AnimeReviews.Count == 0)
                        anime = JMMService.AnidbProcessor.GetAnimeInfoUDP(AnimeID, true);

                    foreach (var animeRev in anime.AnimeReviews)
                    {
                        JMMService.AnidbProcessor.GetReviewUDP(animeRev.ReviewID);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_GetReviews: {0} - {1}", AnimeID, ex.ToString());
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
                AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "AnimeID"));
                ForceRefresh = bool.Parse(TryGetProperty(docCreator, "CommandRequest_GetReviews", "ForceRefresh"));
            }

            return true;
        }

        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_GetReviews_{0}", AnimeID);
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