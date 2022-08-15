using Shoko.Plugin.Abstractions;
using Shoko.Server.Models;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.SignalR.Models
{
    public class EpisodeInfoUpdatedEventSignalRModel
    {
        public EpisodeInfoUpdatedEventSignalRModel(EpisodeInfoUpdatedEventArgs eventArgs)
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(eventArgs.AnimeInfo.AnimeID);
            EpisodeID = ((SVR_AnimeEpisode)eventArgs.EpisodeInfo).AnimeEpisodeID;
            SeriesID = series.AnimeSeriesID;
            GroupID = series.AnimeGroupID;
        }

        /// <summary>
        /// Shoko episode id.
        /// </summary>
        public int EpisodeID { get; set; }

        /// <summary>
        /// Shoko series id.
        /// </summary>
        public int SeriesID { get; set; }

        /// <summary>
        /// Shoko group id.
        /// </summary>
        public int GroupID { get; set; }
    }
}