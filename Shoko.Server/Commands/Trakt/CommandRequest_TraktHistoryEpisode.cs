using System;
using System.Xml;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Commands.Attributes;
using Shoko.Server.Models;
using Shoko.Server.Providers.TraktTV;
using Shoko.Server.Repositories;
using Shoko.Server.Server;
using Shoko.Server.Settings;

namespace Shoko.Server.Commands
{
    [Serializable]
    [Command(CommandRequestType.Trakt_EpisodeHistory)]
    public class CommandRequest_TraktHistoryEpisode : CommandRequestImplementation
    {
        public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

        public TraktSyncAction ActionEnum => (TraktSyncAction) Action;

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority9;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.TraktAddHistory,
            extraParams = new[] {AnimeEpisodeID.ToString()}
        };

        public CommandRequest_TraktHistoryEpisode()
        {
        }

        public CommandRequest_TraktHistoryEpisode(int animeEpisodeID, TraktSyncAction action)
        {
            AnimeEpisodeID = animeEpisodeID;
            Action = (int) action;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        protected override void Process(IServiceProvider serviceProvider)
        {
            logger.Info("Processing CommandRequest_TraktHistoryEpisode: {0}-{1}", AnimeEpisodeID, Action);

            try
            {
                if (!ServerSettings.Instance.TraktTv.Enabled || string.IsNullOrEmpty(ServerSettings.Instance.TraktTv.AuthToken)) return;

                SVR_AnimeEpisode ep = RepoFactory.AnimeEpisode.GetByID(AnimeEpisodeID);
                if (ep != null)
                {
                    TraktSyncType syncType = TraktSyncType.HistoryAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.HistoryRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktHistoryEpisode: {0} - {1}", AnimeEpisodeID,
                    ex);
            }
        }

        /// <summary>
        /// This should generate a unique key for a command
        /// It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TraktHistoryEpisode{AnimeEpisodeID}-{Action}";
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
                AnimeEpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktHistoryEpisode", "AnimeEpisodeID"));
                Action = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktHistoryEpisode", "Action"));
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