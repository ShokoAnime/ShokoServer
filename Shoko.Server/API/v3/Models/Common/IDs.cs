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
        public List<int> TvDBs { get; set; }
        
        // TODO Support for TvDB string IDs (like in the new URLs) one day maybe
        
        /// <summary>
        /// The MovieDB IDs
        /// </summary>
        public List<int> MovieDBs { get; set; }
        
        /// <summary>
        /// The MyAnimeList IDs
        /// </summary>
        public List<int> MALs { get; set; }
        
        /// <summary>
        /// The TraktTv IDs
        /// </summary>
        public List<string> TraktTvs { get; set; }

        /// <summary>
        /// The AniList IDs
        /// </summary>
        public List<int> AniList { get; set; }
        #endregion
    }
}