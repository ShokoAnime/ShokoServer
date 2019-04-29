using System.Collections.Generic;
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
        /// The TvDB IDs
        /// </summary>
        public List<int> TvDB { get; set; } = new List<int>();
        
        // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
        
        /// <summary>
        /// The MovieDB IDs
        /// </summary>
        public List<int> MovieDB { get; set; } = new List<int>();
        
        /// <summary>
        /// The MyAnimeList IDs
        /// </summary>
        public List<int> MAL { get; set; } = new List<int>();
        
        /// <summary>
        /// The TraktTv IDs
        /// </summary>
        public List<string> TraktTv { get; set; } = new List<string>();

        /// <summary>
        /// The AniList IDs
        /// </summary>
        public List<int> AniList { get; set; } = new List<int>();
        #endregion
    }
}