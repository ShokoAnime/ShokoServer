using System;

namespace Shoko.Models.Server
{
    public class AnimeEpisode
    {
        /// <summary>
        /// Local <see cref="AnimeEpisode"/> id.
        /// </summary>
        public int AnimeEpisodeID { get; set; }

        /// <summary>
        /// Local <see cref="AnimeSeries"/> id.
        /// </summary>
        public int AnimeSeriesID { get; set; }

        /// <summary>
        /// The universally unique anidb episode id.
        /// </summary>
        /// <remarks>
        /// Also see <seealso cref="AniDB_Episode"/> for a local representation
        /// of the anidb episode data.
        /// </remarks>
        public int AniDB_EpisodeID { get; set; }

        /// <summary>
        /// Timestamp for when the entry was first created.
        /// </summary>
        public DateTime DateTimeCreated { get; set; }

        /// <summary>
        /// Timestamp for when the entry was last updated.
        /// </summary>
        public DateTime DateTimeUpdated { get; set; }

        /// <summary>
        /// Hidden episodes will not show up in the UI unless explictly
        /// requested, and will also not count towards the unwatched count for
        /// the series.
        /// </summary>
        public bool IsHidden { get; set; }
    }
}
