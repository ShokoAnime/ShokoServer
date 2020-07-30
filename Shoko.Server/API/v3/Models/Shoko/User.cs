using System.Collections.Generic;
using Shoko.Models.Enums;

namespace Shoko.Server.API.v3.Models.Shoko
{
    public class User
    {
        /// <summary>
        /// The UserID, this is used in a lot of v1 and v2 endpoints, and it's needed for editing or removing a user
        /// </summary>
        public int ID { get; set; }

        /// <summary>
        /// Pretty Self-explanatory. It's the Username of the user
        /// </summary>
        public string Username { get; set; }
        
        /// <summary>
        /// Is the user an admin. Admins can perform all operations, including modification of users
        /// </summary>
        public bool IsAdmin { get; set; }
        
        /// <summary>
        /// This is a list of services that the user is set to use. AniDB, Trakt, and Plex, for example
        /// </summary>
        public List<CommunitySites> CommunitySites { get; set; }
        
        /// <summary>
        /// This is also called 'Hide Categories'. The current political atmosphere made me salty enough to call it what it is: a blacklist.
        /// Tags that are here are not visible to the user. Any series with any of these tags will not be shown in any context
        /// </summary>
        public List<string> TagBlacklist { get; set; }

        public class FullUser
        {
            /// <summary>
            /// The password...Shoko is NOT secure, so don't assume this password is safe or even necessary to access the account
            /// </summary>
            public string Password { get; set; }
        }
        
        /// <summary>
        /// The Plex User Settings...
        /// </summary>
        public class PlexUserSettings
        {
            /// <summary>
            /// This means something. Cazzar help me out here.
            /// </summary>
            public string PlexUsers { get; set; }
            /// <summary>
            /// The token for authentication with the Plex Server API
            /// </summary>
            public string PlexToken { get; set; }
        }
    }
}