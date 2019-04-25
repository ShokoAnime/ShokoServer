using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    public class IDs
    {

        /// <summary>
        /// The Shoko internal ID, for easy lookup
        /// </summary>
        [Required]
        public int ID { get; set; }


        #region XRefs

        // These are useful for many things, but for clients, it is mostly auxiliary

        /// <summary>
        /// The AniDB ID
        /// </summary>
        [Required]
        public int AniDB { get; set; }
        
        /// <summary>
        /// The TvDB ID
        /// </summary>
        public int TvDB { get; set; }
        
        // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
        
        /// <summary>
        /// The MovieDB ID
        /// </summary>
        public int MovieDB { get; set; }
        
        /// <summary>
        /// The MyAnimeList ID
        /// </summary>
        public int MAL { get; set; }
        
        /// <summary>
        /// The MyAnimeList ID
        /// </summary>
        public string Trakt { get; set; }

        /// <summary>
        /// The AniList ID
        /// </summary>
        public int AniList { get; set; }
        #endregion
    }
}