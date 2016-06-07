using System;
using System.Globalization;
using System.Threading;
using System.Xml;
using JMMServer.Entities;
using JMMServer.Properties;
using JMMServer.Providers.TraktTV;
using JMMServer.Repositories;

namespace JMMServer.Commands
{
    [Serializable]
    public class CommandRequest_TraktCollectionEpisode : CommandRequestImplementation, ICommandRequest
    {
        public CommandRequest_TraktCollectionEpisode()
        {
        }

        public CommandRequest_TraktCollectionEpisode(int animeEpisodeID, TraktSyncAction action)
        {
            AnimeEpisodeID = animeEpisodeID;
            Action = (int)action;
            CommandType = (int)CommandRequestType.Trakt_EpisodeCollection;
            Priority = (int)DefaultPriority;

            GenerateCommandID();
        }

        public int AnimeEpisodeID { get; set; }
        public int Action { get; set; }

        public TraktSyncAction ActionEnum
        {
            get { return (TraktSyncAction)Action; }
        }

        public CommandRequestPriority DefaultPriority
        {
            get { return CommandRequestPriority.Priority9; }
        }

        public string PrettyDescription
        {
            get
            {
                Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(ServerSettings.Culture);

                return string.Format(Resources.Command_SyncTraktEpisodes, AnimeEpisodeID, Action);
            }
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TraktCollectionEpisode: {0}-{1}", AnimeEpisodeID, Action);

            try
            {
                logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG01");
                if (!ServerSettings.Trakt_IsEnabled || string.IsNullOrEmpty(ServerSettings.Trakt_AuthToken)) return;
                logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG02");

                var repEpisodes = new AnimeEpisodeRepository();
                var ep = repEpisodes.GetByID(AnimeEpisodeID);
                if (ep != null)
                {
                    logger.Info("CommandRequest_TraktCollectionEpisode - DEBUG03");
                    var syncType = TraktSyncType.CollectionAdd;
                    if (ActionEnum == TraktSyncAction.Remove) syncType = TraktSyncType.CollectionRemove;
                    TraktTVHelper.SyncEpisodeToTrakt(ep, syncType);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Error processing CommandRequest_TraktCollectionEpisode: {0} - {1} - {2}", AnimeEpisodeID,
                    Action, ex.ToString());
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
                AnimeEpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktCollectionEpisode", "AnimeEpisodeID"));
                Action = int.Parse(TryGetProperty(docCreator, "CommandRequest_TraktCollectionEpisode", "Action"));
            }

            return true;
        }

        /// <summary>
        ///     This should generate a unique key for a command
        ///     It will be used to check whether the command has already been queued before adding it
        /// </summary>
        public override void GenerateCommandID()
        {
            CommandID = string.Format("CommandRequest_TraktCollectionEpisode{0}-{1}", AnimeEpisodeID, Action);
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