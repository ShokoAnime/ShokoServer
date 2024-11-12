namespace Shoko.Server.Providers.TraktTV;

public static class TraktURIs
{
    public const string Oauth = TraktConstants.BaseAPIURL + @"/oauth/token";

    public const string OAuthDeviceCode = TraktConstants.BaseAPIURL + @"/oauth/device/code";
    public const string OAuthDeviceToken = TraktConstants.BaseAPIURL + @"/oauth/device/token";

    // Website links
    // http://docs.trakt.apiary.io/#introduction/website-media-links
    public const string WebsiteShow = TraktConstants.BaseWebsiteURL + @"/shows/{0}";

    public const string WebsiteMovie = TraktConstants.BaseWebsiteURL + @"/movies/{0}"; // /shows/:slug/seasons/:num

    public const string WebsiteSeason = TraktConstants.BaseWebsiteURL + @"/shows/{0}/seasons/{1}";
    // /shows/:slug/seasons/:num

    public const string WebsiteEpisode = TraktConstants.BaseWebsiteURL + @"/shows/{0}/seasons/{1}/episodes/{2}";
    // /shows/:slug/seasons/:num/episodes/:num

    //types
    // movie , show , episode , person , list 
    public const string SearchByQuery = TraktConstants.BaseAPIURL + @"/search?fields=title&type={0}&query={1}"; // /search?fields=title&type=:type&query=:query
    // search criteria / search type

    // trakt-movie , trakt-show , trakt-episode , imdb , tmdb 
    public const string SearchByID = TraktConstants.BaseAPIURL + @"/search/{0}/{1}?type={2}"; // /search/:provider/:id?type=:type

    // http://docs.trakt.apiary.io/#reference/shows/summary/get-a-single-show
    // {0} trakt ID, trakt slug, or IMDB ID Example: game-of-thrones
    public const string ShowSummary = TraktConstants.BaseAPIURL + @"/shows/{0}?extended=full";

    // http://docs.trakt.apiary.io/#reference/seasons/summary/get-all-seasons-for-a-show
    // {0} trakt ID, trakt slug, or IMDB ID Example: game-of-thrones
    public const string ShowSeasons = TraktConstants.BaseAPIURL + @"/shows/{0}/seasons?extended=episodes";

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

    public const string SetScrobbleStart = TraktConstants.BaseAPIURL + @"/scrobble/start";
    public const string SetScrobblePause = TraktConstants.BaseAPIURL + @"/scrobble/pause";
    public const string SetScrobbleStop = TraktConstants.BaseAPIURL + @"/scrobble/stop";
}
