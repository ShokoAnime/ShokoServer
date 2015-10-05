using System.Collections.Generic;

namespace JMMModels.Childs
{

    public class BaseAuthorization
    {
        public AuthorizationProvider Provider { get; set; }
    }

    public class UserNameAuthorization : BaseAuthorization
    {
        public string UserName { get; set; }
        public string Password { get; set; }
    }

    public class TraktAuthorization : BaseAuthorization
    {
        public string Trakt_AuthToken { get; set; }
        public string Trakt_RefreshToken { get; set; }
        public string Trakt_TokenExpirationDate { get; set; }
    }

    public class AniDBAuthorization : UserNameAuthorization
    {
        public string AniDB_AVDumpKey { get; set; }
    }
}
