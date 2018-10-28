namespace Shoko.Server.API.v3
{
    /// <summary>
    /// Description object. Lots of data that is future-proofed, just in case there are more languages or sources added
    /// </summary>
    public class Description
    {
        // The actual description, not sanitized for urls and such
        public string description { get; set; }
            
        // Probably AniDB, TvDB, or AniList for the foreseeable future
        public string source { get; set; }
            
        // Language, USE TITLE LANGUAGE CODES! maybe one day it'll be more than just english.
        public string language { get; set; }
    }
}