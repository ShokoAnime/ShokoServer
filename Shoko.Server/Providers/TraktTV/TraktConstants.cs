namespace Shoko.Server.Providers.TraktTV
{
    public static class TraktConstants
    {
        public const int PaginationLimit = 10;

        // Production
        public const string ClientID = "a20707fa9666bea4acd86bc6ea2123bd6ffdbe996b4927cfdba96f4d3fca3042";
        public const string ClientSecret = "7ef5eec766070fa0b34a4a4a2fea2ad0ddbe9bb1bc1e8eb621551c52fb288739";
        public const string BaseAPIURL = @"https://api-v2launch.trakt.tv";
        public const string BaseWebsiteURL = @"https://trakt.tv";
        public const string PINAuth = BaseWebsiteURL + @"/pin/5309";

        // Staging
        //public const string ClientID = "5f6cb4edf31210042e5f2ab2eaa9e5d0e87116936aabde763cd4c885fea4fd76";
        //public const string ClientSecret = "d023b70cc0e8c5e18026c71f4dcdf8ca98e376288eaf9c3e1869d1b15c969d3b";
        //public const string BaseAPIURL = @"http://api.staging.trakt.tv"; // staging
        //public const string BaseWebsiteURL = @"https://trakt.tv";
        //public const string PINAuth = BaseWebsiteURL + @"/pin/600";
    }

    public static class TraktSearchType
    {
        // movie , show , episode , person , list 
        public const string movie = "movie";
        public const string show = "show";
        public const string episode = "episode";
        public const string person = "person";
        public const string list = "list";
    }

    // trakt-movie , trakt-show , trakt-episode , imdb , tmdb , tvdb , tvrage
    public static class TraktSearchIDType
    {
        // movie , show , episode , person , list 
        public const string traktmovie = "trakt-movie";
        public const string traktshow = "trakt-show";
        public const string traktepisode = "trakt-episode";
        public const string imdb = "imdb";
        public const string tmdb = "tmdb";
        public const string tvdb = "tvdb";
        public const string tvrage = "tvrage";
    }

    public enum TraktSyncType
    {
        CollectionAdd = 1,
        CollectionRemove = 2,
        HistoryAdd = 3,
        HistoryRemove = 4
    }

    public enum ScrobblePlayingStatus
    {
        Start = 1,
        Pause = 2,
        Stop = 3
    }

    public enum ScrobblePlayingType
    {
        movie = 1,
        episode = 2
    }

    public enum SearchIDType
    {
        Show = 1,
        Episode = 2
    }

    public static class TraktStatusCodes
    {
        // http://docs.trakt.apiary.io/#introduction/status-codes
        // Status Codes

        public const int Success = 200;
        public const int Success_Post = 201;
        public const int Success_Delete = 204;

        public const int Bad_Request = 400;
        public const int Unauthorized = 401;
        public const int Forbidden = 403;

        public const int Not_Found = 404;
        public const int Method_Not_Found = 405;
        public const int Conflict = 409;

        public const int Precondition_Failed = 412;
        public const int Unprocessable_Entity = 422;
        public const int Rate_Limit_Exceeded = 429;

        public const int Server_Error = 500;
        public const int Service_Unavailable = 503;
        public const int Service_Unavailable2 = 520;
        public const int Service_Unavailable3 = 521;
        public const int Service_Unavailable4 = 522;
    }
}