namespace Shoko.Server.Providers.TraktTV
{
    public static class TraktURIs
    {
        public const string Oauth = TraktConstants.BaseAPIURL + @"/oauth/token";


        // Website links
        // http://docs.trakt.apiary.io/#introduction/website-media-links
        public const string WebsiteShow = TraktConstants.BaseWebsiteURL + @"/shows/{0}";
        public const string WebsiteMovie = TraktConstants.BaseWebsiteURL + @"/movies/{0}"; // /shows/:slug/seasons/:num

        public const string WebsiteSeason = TraktConstants.BaseWebsiteURL + @"/shows/{0}/seasons/{1}";
        // /shows/:slug/seasons/:num

        public const string WebsiteEpisode = TraktConstants.BaseWebsiteURL + @"/shows/{0}/seasons/{1}/episodes/{2}";
        // /shows/:slug/seasons/:num/episodes/:num

        public const string WebsitePerson = TraktConstants.BaseWebsiteURL + @"/people/{0}"; // /people/:slug
        public const string WebsiteComment = TraktConstants.BaseWebsiteURL + @"/comments/{0}"; // /comments/:id

        //types
        // movie , show , episode , person , list 
        public const string Search = TraktConstants.BaseAPIURL + @"/search?query={0}&type={1}";
        // search criteria / search type

        // trakt-movie , trakt-show , trakt-episode , imdb , tmdb , tvdb , tvrage 
        public const string SearchByID = TraktConstants.BaseAPIURL + @"/search?id_type={0}&id={1}"; // id type / id

        // http://docs.trakt.apiary.io/#reference/shows/summary/get-a-single-show
        // {0} trakt ID, trakt slug, or IMDB ID Example: game-of-thrones
        public const string ShowSummary = TraktConstants.BaseAPIURL + @"/shows/{0}?extended=full,images";

        // http://docs.trakt.apiary.io/#reference/seasons/summary/get-all-seasons-for-a-show
        // {0} trakt ID, trakt slug, or IMDB ID Example: game-of-thrones
        public const string ShowSeasons = TraktConstants.BaseAPIURL + @"/shows/{0}/seasons?extended=episodes,images";

        // get comments for a show
        // http://docs.trakt.apiary.io/#reference/shows/watched-progress/get-all-show-comments
        public const string ShowComments = TraktConstants.BaseAPIURL + @"/shows/{0}/comments?page={1}&limit={2}";

        // get friends
        // http://docs.trakt.apiary.io/#reference/users/followers/get-friends
        public const string GetUserFriends = TraktConstants.BaseAPIURL + @"/users/me/friends";

        // get friends watched history
        // http://docs.trakt.apiary.io/#reference/users/history/get-watched-history
        public const string GetUserHistory = TraktConstants.BaseAPIURL + @"/users/{0}/history/episodes";

        // sync collection (add to collection)
        // useds for movies, series, episodes
        // http://docs.trakt.apiary.io/#reference/sync/add-to-collection/add-items-to-collection
        public const string SyncCollectionAdd = TraktConstants.BaseAPIURL + @"/sync/collection";

        // sync collection (remove from collection)
        // useds for movies, series, episodes
        // http://docs.trakt.apiary.io/#reference/sync/remove-from-collection
        public const string SyncCollectionRemove = TraktConstants.BaseAPIURL + @"/sync/collection/remove";

        // add to history (mark as watched)
        // useds for movies, series, episodes
        // http://docs.trakt.apiary.io/#reference/sync/add-to-history/add-items-to-watched-history?console=1
        public const string SyncHistoryAdd = TraktConstants.BaseAPIURL + @"/sync/history";

        // remove from history (mark as un-watched)
        // useds for movies, series, episodes
        // http://docs.trakt.apiary.io/#reference/sync/remove-from-history/remove-items-from-history?console=1
        public const string SyncHistoryRemove = TraktConstants.BaseAPIURL + @"/sync/history/remove";

        // post a comment (shout or review)
        // useds for movies, series, episodes
        // http://docs.trakt.apiary.io/#reference/sync/get-watched/get-watched
        public const string PostComment = TraktConstants.BaseAPIURL + @"/comments";

        // get all the shows and episodes a user has watched
        // http://docs.trakt.apiary.io/#reference/users/history/get-watched-history
        public const string GetWatchedShows = TraktConstants.BaseAPIURL + @"/sync/watched/shows";

        // get all the shows and episodes a user has collected
        // http://docs.trakt.apiary.io/#reference/sync/get-collection/get-collection
        public const string GetCollectedShows = TraktConstants.BaseAPIURL + @"/sync/collection/shows";

        //public const string RatedMovies = @"http://api-v2launch.trakt.tv/sync/ratings/movies";
        //public const string RatedShows = @"http://api-v2launch.trakt.tv/sync/ratings/shows";
        //public const string RatedEpisodes = @"http://api-v2launch.trakt.tv/sync/ratings/episodes";
        //public const string RatedSeasons = @"http://api-v2launch.trakt.tv/sync/ratings/seasons";

        //public const string WatchedMovies = @"http://api-v2launch.trakt.tv/sync/watched/movies";
        //public const string WatchedShows = @"http://api-v2launch.trakt.tv/sync/watched/shows";

        //public const string CollectedMovies = @"http://api-v2launch.trakt.tv/sync/collection/movies";
        //public const string CollectedShows = @"http://api-v2launch.trakt.tv/sync/collection/shows";

        //public const string WatchlistMovies = @"http://api-v2launch.trakt.tv/sync/watchlist/movies";
        //public const string WatchlistShows = @"http://api-v2launch.trakt.tv/sync/watchlist/shows";
        //public const string WatchlistEpisodes = @"http://api-v2launch.trakt.tv/sync/watchlist/episodes";
        //public const string WatchlistSeasons = @"http://api-v2launch.trakt.tv/sync/watchlist/seasons";

        //public const string SyncRatings = @"http://api-v2launch.trakt.tv/sync/ratings";
        //public const string SyncWatchlist = @"http://api-v2launch.trakt.tv/sync/watchlist";
        //public const string SyncWatched = @"http://api-v2launch.trakt.tv/sync/history";
        //public const string SyncWatchedRemove = "https://api-v2launch.trakt.tv/sync/history/remove";
        //public const string SyncCollectionRemove = "https://api-v2launch.trakt.tv/sync/collection/remove";
        //public const string SyncRatingsRemove = "https://api-v2launch.trakt.tv/sync/ratings/remove";
        //public const string SyncWatchlistRemove = "https://api-v2launch.trakt.tv/sync/watchlist/remove";

        public const string SetScrobbleStart = TraktConstants.BaseAPIURL + @"/scrobble/start";
        public const string SetScrobblePause = TraktConstants.BaseAPIURL + @"/scrobble/pause";
        public const string SetScrobbleStop = TraktConstants.BaseAPIURL + @"/scrobble/stop";
    }
}