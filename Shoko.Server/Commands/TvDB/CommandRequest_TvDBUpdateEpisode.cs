using System;
using System.Linq;
using System.Xml;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Models.Server;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateEpisode : CommandRequestImplementation, ICommandRequest
    {
        public int TvDBEpisodeID { get; set; }
        public bool ForceRefresh { get; set; }
        public bool DownloadImages { get; set; }
        public string InfoString { get; set; }

        public CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority8;

        public QueueStateStruct PrettyDescription => new QueueStateStruct
        {
            queueState = QueueStateEnum.GettingTvDBEpisode,
            extraParams = new[] {$"{InfoString} ({TvDBEpisodeID})"}
        };

        public CommandRequest_TvDBUpdateEpisode()
        {
        }

        public CommandRequest_TvDBUpdateEpisode(int tvDbEpisodeID, string infoString, bool downloadImages, bool forced)
        {
            TvDBEpisodeID = tvDbEpisodeID;
            ForceRefresh = forced;
            DownloadImages = downloadImages;
            InfoString = infoString;
            CommandType = (int) CommandRequestType.TvDB_UpdateEpisode;
            Priority = (int) DefaultPriority;

            GenerateCommandID();
        }

        public override void ProcessCommand()
        {
            logger.Info("Processing CommandRequest_TvDBUpdateEpisode: {0} ({1})", InfoString, TvDBEpisodeID);

            try
            {
                TvDBApiHelper.UpdateEpisode(TvDBEpisodeID, DownloadImages, ForceRefresh);
                var ep = RepoFactory.TvDB_Episode.GetByTvDBID(TvDBEpisodeID);
                var xref = RepoFactory.CrossRef_AniDB_TvDBV2.GetByTvDBID(ep.SeriesID).DistinctBy(a => a.AnimeID);
                if (xref == null) return;
                foreach (var crossRefAniDbTvDbv2 in xref)
                {
                    var anime = RepoFactory.AnimeSeries.GetByAnimeID(crossRefAniDbTvDbv2.AnimeID);
                    if (anime == null) continue;
                    var episodes = RepoFactory.AnimeEpisode.GetBySeriesID(anime.AnimeSeriesID);
                    foreach (SVR_AnimeEpisode episode in episodes)
                    {
                        // Save
                        if ((episode.TvDBEpisode?.Id ?? TvDBEpisodeID) != TvDBEpisodeID) continue;
                        episode.TvDBEpisode = null;
                        RepoFactory.AnimeEpisode.Save(episode);
                    }
                    anime.QueueUpdateStats();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Error Processing CommandRequest_TvDBUpdateEpisode: {0} ({1})", InfoString, TvDBEpisodeID);
            }
        }

        public override void GenerateCommandID()
        {
            CommandID = $"CommandRequest_TvDBUpdateEpisodes{TvDBEpisodeID}";
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
                XmlDocument docCreator = new XmlDocument();
                docCreator.LoadXml(CommandDetails);

                // populate the fields
                TvDBEpisodeID =
                    int.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateEpisode", "TvDBEpisodeID"));
                ForceRefresh =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateEpisode",
                        "ForceRefresh"));
                DownloadImages =
                    bool.Parse(TryGetProperty(docCreator, "CommandRequest_TvDBUpdateEpisode",
                        "DownloadImages"));
                InfoString =
                    TryGetProperty(docCreator, "CommandRequest_TvDBUpdateEpisode",
                        "InfoString");
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