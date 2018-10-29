using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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
        [Required]
        public int id { get; set; }

        /// <summary>
        /// AniDB_ID
        /// </summary>
        [Required]
        public int anidb_id { get; set; }

        /// <summary>
        /// The group ID, for easy lookup
        /// While required, it is possible (a problem but possible) for a series to not have a group
        /// In this case, the id will be 0
        /// </summary>
        [Required]
        public int group_id { get; set; }

        /// <summary>
        /// Is it porn...or close enough
        /// If not provided, assume no
        /// </summary>
        public bool restricted { get; set; }

        /// <summary>
        /// The server's title. This will use overrides, then the naming settings, then MainTitle if all else fails. This is a guaranteed fallback
        /// </summary>
        [Required]
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
        /// The default poster, with override
        /// </summary>
        public Image preferred_poster { get; set; }
        
        /// <summary>
        /// The rest of the images, including posters, fanarts, and banners
        /// They have the fields for the client to filter on
        /// </summary>
        public List<Image> images { get; set; }

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
        
        /// <summary>
        /// series cast and staff
        /// </summary>
        public List<Role> cast { get; set; }

        /// <summary>
        /// links to series pages on various sites
        /// </summary>
        public List<Resource> resources { get; set; }

        /// <summary>
        /// A site link, as in hyperlink.
        /// </summary>
        public class Resource
        {
            /// <summary>
            /// site name
            /// </summary>
            [Required]
            public string name { get; set; }

            /// <summary>
            /// the url to the series page
            /// </summary>
            [Required]
            public string url { get; set; }

            /// <summary>
            /// favicon or something. A logo
            /// </summary>
            public Image image { get; set; }
        }
    }
}