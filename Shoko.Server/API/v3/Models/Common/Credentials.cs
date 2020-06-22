namespace Shoko.Server.API.v3
{
    public class Credentials
    {
        /// <summary>
        /// A generic Username field
        /// </summary>
        public string Username { get; set; }
        /// <summary>
        /// A generic password field
        /// </summary>
        public string Password { get; set; }
        
        /// <summary>
        /// Credentials Specific to AniDB
        /// </summary>
        public class AniDBCredentials : Credentials
        {
            /// <summary>
            /// The client port. default 4556
            /// </summary>
            public int ClientPort { get; set; }
            
            /// <summary>
            /// The AVDump port. default 4557
            /// </summary>
            public int AVDumpClientPort { get; set; }
            
            /// <summary>
            /// The AVDump key is set on the AniDB profile page
            /// </summary>
            public string AVDumpAPIKey { get; set; }
        }
    }
}