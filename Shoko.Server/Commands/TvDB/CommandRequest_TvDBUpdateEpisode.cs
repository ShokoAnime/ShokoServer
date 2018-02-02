using System;
using System.Xml;
using Shoko.Commons.Extensions;
using Shoko.Commons.Queue;
using Shoko.Models.Queue;
using Shoko.Server.Models;
using Shoko.Server.Providers.TvDB;
using Shoko.Server.Repositories;

namespace Shoko.Server.Commands
{
    [Serializable]
    public class CommandRequest_TvDBUpdateEpisode : CommandRequest_TvDBBase
    {
        public virtual int TvDBEpisodeID { get; set; }
        public virtual bool ForceRefresh { get; set; }
        public virtual bool DownloadImages { get; set; }
        public virtual string InfoString { get; set; }

        public override CommandRequestPriority DefaultPriority => CommandRequestPriority.Priority6;

        public override QueueStateStruct PrettyDescription => new QueueStateStruct
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
                var ep = Repo.TvDB_Episode.GetByTvDBID(TvDBEpisodeID);
                var xref = Repo.CrossRef_AniDB_TvDBV2.GetByTvDBID(ep.SeriesID).DistinctBy(a => a.AnimeID);
                if (xref == null) return;
                foreach (var crossRefAniDbTvDbv2 in xref)
                {
                    var anime = Repo.AnimeSeries.GetByAnimeID(crossRefAniDbTvDbv2.AnimeID);
                    if (anime == null) continue;
                    using (var upd = Repo.AnimeEpisode.BeginBatchUpdate(() => Repo.AnimeEpisode.GetBySeriesID(anime.AnimeSeriesID)))
                    {
                        foreach (SVR_AnimeEpisode episode in upd)
                        {
                            // Save
                            if ((episode.TvDBEpisode?.Id ?? TvDBEpisodeID) != TvDBEpisodeID) continue;
                            episode.TvDBEpisode = null;
                            upd.Update(episode);
                        }
                        upd.Commit();
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

        public override bool InitFromDB(Shoko.Models.Server.CommandRequest cq)
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
    }
}