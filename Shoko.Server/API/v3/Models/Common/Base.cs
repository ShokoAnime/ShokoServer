using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    public abstract class Base
    {
        /// <summary>
        /// the internal ID, such as AnimeSeriesID or AnimeGroupID
        /// </summary>
        [Required]
        public int id { get; set; }
        
        /// <summary>
        /// the type, series, group, etc. Just makes it easier for the client to know exactly what they are looking at
        /// </summary>
        [Required]
        public abstract string type { get; }
        
        /// <summary>
        /// The server's title. This will use overrides, the naming settings, MainTitle if all else fails. This is a guaranteed fallback
        /// </summary>
        [Required]
        public string name { get; set; }
        
        /// <summary>
        /// number of direct children (number of series in group, eps in series, etc)
        /// </summary>
        [Required]
        public int size { get; set; }
        
        /// <summary>
        /// Sizes object, has totals
        /// </summary>
        public Sizes sizes { get; set; }
    }
}