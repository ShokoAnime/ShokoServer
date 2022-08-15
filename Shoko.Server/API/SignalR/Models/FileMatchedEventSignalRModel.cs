using System.Collections.Generic;
using System.Linq;
using Shoko.Plugin.Abstractions;
using Shoko.Server.Models;

namespace Shoko.Server.API.SignalR.Models
{
    public class FileMatchedEventSignalRModel : FileEventSignalRModel
    {
        public FileMatchedEventSignalRModel(FileMatchedEventArgs eventArgs) : base(eventArgs)
        {
            var episodes = eventArgs.EpisodeInfo
                .Cast<SVR_AnimeEpisode>();
            var seriesToGroupDict = episodes
                .GroupBy(e => e.AnimeSeriesID)
                .Select(e => e.FirstOrDefault().GetAnimeSeries())
                .ToDictionary(s => s.AnimeSeriesID, s => s.AnimeGroupID);
            CrossRefs = episodes
                .Select(e => new FileMatchedCrossRef()
                    {
                        EpisodeID = e.AnimeEpisodeID,
                        SeriesID = e.AnimeSeriesID,
                        GroupID = seriesToGroupDict[e.AnimeSeriesID],
                    }
                )
                .ToList();
        }

        /// <summary>
        /// Cross references of episodes linked to this file.
        /// </summary>
        public List<FileMatchedCrossRef> CrossRefs { get; set; }
    }

    public class FileMatchedCrossRef
    {
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