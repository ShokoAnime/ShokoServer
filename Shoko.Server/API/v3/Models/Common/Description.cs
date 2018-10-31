using System.ComponentModel.DataAnnotations;

namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Description object. Lots of data that is future-proofed, just in case there are more languages or sources added
    /// </summary>
    public class Description
    {
        // The actual description, not sanitized for urls and such
        [Required]
        public string description { get; set; }
            
        // Probably AniDB, TvDB, or AniList for the foreseeable future
        [Required]
        public string source { get; set; }
            
        // Language, USE TITLE LANGUAGE CODES! maybe one day it'll be more than just english.
        [Required]
        public string language { get; set; }
    }
}