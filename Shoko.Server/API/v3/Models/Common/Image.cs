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
        public string type { get; set; }
        
        /// <summary>
        /// normally a client won't need this, but if the client plans to set it as default, disabled, deleted, etc, then it'll be needed
        /// </summary>
        public int id { get; set; }
        
        public string source { get; set; }
    }
}