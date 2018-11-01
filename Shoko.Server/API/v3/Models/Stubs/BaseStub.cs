using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    public abstract class BaseStub
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
    }
}