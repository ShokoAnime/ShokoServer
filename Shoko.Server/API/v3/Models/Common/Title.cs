using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Title object, stores the title, type, language, and source
    /// if using a TvDB title, assume "eng:official". If using AniList, assume "x-jat:main"
    /// AniDB's MainTitle is "x-jat:main"
    /// </summary>
    public class Title
    {
        /// <summary>
        /// the title
        /// </summary>
        [Required]
        public string title { get; set; }
            
        /// <summary>
        /// convert to AniDB style (x-jat is the special one, but most are standard 3-digit short names)
        /// </summary>
        [Required]
        public string language { get; set; }

        /// <summary>
        /// AniDB type
        /// </summary>
        public string type { get; set; }
            
        /// <summary>
        /// AniDB, TvDB, AniList, etc
        /// </summary>
        [Required]
        public string source { get; set; }
    }
}