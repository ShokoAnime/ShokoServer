using Shoko.Plugin.Abstractions;
using Shoko.Server.Repositories;

namespace Shoko.Server.API.SignalR.Models
{
    public class SeriesInfoUpdatedEventSignalRModel
    {
        public SeriesInfoUpdatedEventSignalRModel(SeriesInfoUpdatedEventArgs eventArgs)
        {
            var series = RepoFactory.AnimeSeries.GetByAnimeID(eventArgs.AnimeInfo.AnimeID);
            SeriesID = series.AnimeSeriesID;
            GroupID = series.AnimeGroupID;
        }

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