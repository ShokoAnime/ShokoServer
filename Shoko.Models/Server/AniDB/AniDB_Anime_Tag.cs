namespace Shoko.Models.Server
{
    public class AniDB_Anime_Tag
    {
        /// <summary>
        /// Local anidb anime &lt;-&gt; anidb tag cross-reference id.
        /// </summary>
        public int AniDB_Anime_TagID { get; set; }

        /// <summary>
        /// Anidb anime id.
        /// </summary>
        public int AnimeID { get; set; }

        /// <summary>
        /// Anidb tag id.
        /// </summary>
        public int TagID { get; set; }

        /// <summary>
        /// Is set to 1 if this tag is considered as a spoiler for the anime it
        /// is attached to.
        /// </summary>
        public bool LocalSpoiler { get; set; }

        /// <summary>
        /// The tag weight. A value of 0 means the tag is considered
        /// "weightless."
        /// </summary>
        public int Weight { get; set; }
    }
}
