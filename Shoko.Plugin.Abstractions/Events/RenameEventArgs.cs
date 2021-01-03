using System.Collections.Generic;
using System.ComponentModel;
using Shoko.Plugin.Abstractions.DataModels;

namespace Shoko.Plugin.Abstractions
{
    public class RenameEventArgs : CancelEventArgs
    {
        /// <summary>
        /// Information about the file itself, such as MediaInfo
        /// </summary>
        public IVideoFile FileInfo { get; set; }

        /// <summary>
        /// Information about the Anime, such as titles
        /// </summary>
        public IList<IAnime> AnimeInfo { get; set; }

        /// <summary>
        /// Information about the group
        /// </summary>
        public IList<IGroup> GroupInfo { get; set; }

        /// <summary>
        /// Information about the episode, such as titles
        /// </summary>
        public IList<IEpisode> EpisodeInfo { get; set; }
    }
}