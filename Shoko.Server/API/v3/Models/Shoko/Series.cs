using System.Collections.Generic;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Series object, stores all of the series info
    /// </summary>
    public class Series
    {
        /// <summary>
        /// AnimeSeriesID
        /// </summary>
        public int id { get; set; }
        
        /// <summary>
        /// AniDB_ID
        /// </summary>
        public int anidb_id { get; set; }
        
        /// <summary>
        /// The group ID, for easy lookup
        /// </summary>
        public int group_id { get; set; }
        
        /// <summary>
        /// The server's title. This will use overrides, then the naming settings, then MainTitle if all else fails. This is a guaranteed fallback
        /// </summary>
        public string preferred_title { get; set; }
        
        /// <summary>
        /// There should always be at least one of these, since preferred_title will be valid
        /// </summary>
        public List<Title> titles { get; set; }
        
        /// <summary>
        /// Description, probably only 2 or 3
        /// </summary>
        public List<Description> description { get; set; }
        
        /// <summary>
        /// The ratings object, with info of ratings from various sources
        /// </summary>
        public List<Rating> ratings { get; set; }
        
        /// <summary>
        /// the user's rating
        /// </summary>
        public Rating user_rating { get; set; }
        
        /// <summary>
        /// tags
        /// </summary>
        public List<string> tags { get; set; }
        
    }
}