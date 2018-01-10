using System;
using System.Collections.Generic;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_MALUpdatedWatchedStatus : CommandRequest
    {
        public virtual int AnimeID { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.UpdateMALWatched,
            extraParams = new[] {AnimeID.ToString()}
        };

        public CommandRequest_MALUpdatedWatchedStatus()
        {
        }

        public CommandRequest_MALUpdatedWatchedStatus(int animeID)
        {
            AnimeID = animeID;
            CommandType = (int) CommandRequestType.MAL_UpdateStatus;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALUpdatedWatchedStatus: {0}", AnimeID);

            try
            {
                // find the latest eps to update
                SVR_AniDB_Anime anime = Repo.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                List<CrossRef_AniDB_MAL> crossRefs = anime.GetCrossRefMAL();
                if (crossRefs == null || crossRefs.Count == 0)
                    return;

                SVR_AnimeSeries ser = Repo.AnimeSeries.GetByAnimeID(AnimeID);
                if (ser == null) return;

                MALHelper.UpdateMALSeries(ser);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALUpdatedWatchedStatus: {0} - {1}", AnimeID,
                    ex);
            }
        }

        private int GetUpperEpisodeLimit(List<CrossRef_AniDB_MAL> crossRefs, CrossRef_AniDB_MAL xrefBase)
        {
            foreach (CrossRef_AniDB_MAL xref in crossRefs)
            {
                if (xref.StartEpisodeType == xrefBase.StartEpisodeType)
                {
                    if (xref.StartEpisodeNumber > xrefBase.StartEpisodeNumber)
                        return xref.StartEpisodeNumber - 1;
                }
            }

            return int.MaxValue;
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_MALUpdatedWatchedStatus_{AnimeID}";
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
                AnimeID = int.Parse(
                    TryGetProperty(docCreator, "CommandRequest_MALUpdatedWatchedStatus", "AnimeID"));
            }

            return true;
        }
    }
}