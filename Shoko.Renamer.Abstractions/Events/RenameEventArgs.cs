using System.ComponentModel;
using Shoko.Renamer.Abstractions.DataModels;

namespace Shoko.Renamer.Abstractions
{
    public class RenameEventArgs : CancelEventArgs
    {
        /// <summary>
        /// The final name of the file
        /// </summary>
        public string Result { get; set; }

        /// <summary>
        /// Information about the file itself, such as MediaInfo
        /// </summary>
        public IVideoFile FileInfo { get; set; }

        /// <summary>
        /// Information about the Anime, such as titles
        /// </summary>
        public IAnime AnimeInfo { get; set; }
    }
}