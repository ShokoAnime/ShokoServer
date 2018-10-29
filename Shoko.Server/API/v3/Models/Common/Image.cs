using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Image container
    /// </summary>
    public class Image
    {
        /// <summary>
        /// text representation of type of image. fanart, poster, etc. Mainly so clients know what they are getting
        /// </summary>
        [Required]
        public string type { get; set; }
        
        /// <summary>
        /// normally a client won't need this, but if the client plans to set it as default, disabled, deleted, etc, then it'll be needed
        /// </summary>
        [Required]
        public int id { get; set; }
        
        /// <summary>
        /// AniDB, TvDB, MovieDB, etc
        /// </summary>
        [Required]
        public string source { get; set; }
        
        /// <summary>
        /// The relative path from the base image directory. A client with access to the server's filesystem can map
        /// these for quick access and no need for caching
        /// </summary>
        public string relative_filepath { get; set; }
    }
}