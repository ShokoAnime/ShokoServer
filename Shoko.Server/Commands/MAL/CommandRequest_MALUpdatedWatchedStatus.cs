using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Xml;
using Shoko.Server.Repositories.Cached;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.MyAnimeList;
using Shoko.Server.Repositories;


namespace Shoko.Server.Commands.MAL
{
    [Serializable]
    public class CommandRequest_MALUpdatedWatchedStatus : CommandRequestImplementation, ICommandRequest
    {
        public int AnimeID { get; set; }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public QueueStateStruct PrettyDescription
        {
            get
            {
                return new QueueStateStruct() { queueState = QueueStateEnum.UpdateMALWatched, extraParams = new string[] { AnimeID.ToString() }  };
            }
        }

        public CommandRequest_MALUpdatedWatchedStatus()
        {
        }

        public CommandRequest_MALUpdatedWatchedStatus(int animeID)
        {
            this.AnimeID = animeID;
            this.CommandType = (int) CommandRequestType.MAL_UpdateStatus;
            this.Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_MALUpdatedWatchedStatus: {0}", AnimeID);

            try
            {
                // find the latest eps to update
                SVR_AniDB_Anime anime = RepoFactory.AniDB_Anime.GetByAnimeID(AnimeID);
                if (anime == null) return;

                List<CrossRef_AniDB_MAL> crossRefs = anime.GetCrossRefMAL();
                if (crossRefs == null || crossRefs.Count == 0)
                    return;

                SVR_AnimeSeries ser = RepoFactory.AnimeSeries.GetByAnimeID(AnimeID);
                if (ser == null) return;

                MALHelper.UpdateMALSeries(ser);
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_MALUpdatedWatchedStatus: {0} - {1}", AnimeID,
                    ex.ToString());
                return;
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
            this.CommandID = string.Format("CommandRequest_MALUpdatedWatchedStatus_{0}", this.AnimeID);
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
                this.AnimeID = int.Parse(TryGetProperty(docCreator, "CommandRequest_MALUpdatedWatchedStatus", "AnimeID"));
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