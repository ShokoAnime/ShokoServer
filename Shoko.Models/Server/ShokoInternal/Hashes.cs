namespace Shoko.Models.Server
{
    public class Hashes
    {
        /// <summary>
        /// ED2K is AniDB's base hash. It is used in any place where "Hash" is said without context
        /// </summary>
        public string ED2K { get; set; }
        
        /// <summary>
        /// SHA1 is not used internally, but it is effortless to calculate with the others
        /// </summary>
        public string SHA1 { get; set; }
        
        /// <summary>
        /// CRC. It's got plenty of uses, but the big one is checking for file corruption
        /// </summary>
        public string CRC32 { get; set; }
        
        /// <summary>
        /// MD5 might be useful for clients, but it's not used internally
        /// </summary>
        public string MD5 { get; set; }
    }
}