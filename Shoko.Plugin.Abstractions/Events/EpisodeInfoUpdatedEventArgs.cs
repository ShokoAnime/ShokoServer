using Shoko.Plugin.Abstractions.DataModels;
using Shoko.Plugin.Abstractions.Enums;

namespace Shoko.Plugin.Abstractions
{

    /// <summary>
    /// Currently, these will fire a lot in succession, as these are updated in batch with a series.
    /// </summary>
    public class EpisodeInfoUpdatedEventArgs
    {
        /// <summary>
        /// Where the data was updated. If there was a batch operation, this may just say the first one.
        /// </summary>
        public DataSourceEnum Type { get; set; }
        /// <summary>
        /// This is the full data. A diff was not performed for this.
        /// This is provided for convenience, use <see cref="IShokoEventHandler.SeriesUpdate"/>
        /// </summary>
        public IAnime AnimeInfo { get; set; }
        /// <summary>
        /// This is the full data. A diff was not performed for this.
        /// </summary>
        public IEpisode EpisodeInfo { get; set; }
    }
}
