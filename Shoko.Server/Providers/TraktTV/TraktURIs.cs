namespace Shoko.Server.Providers.TraktTV;

public static class TraktURIs
{
    public const string Oauth = TraktConstants.BaseAPIURL + @"/oauth/token";

    public const string OAuthDeviceCode = TraktConstants.BaseAPIURL + @"/oauth/device/code";
    public const string OAuthDeviceToken = TraktConstants.BaseAPIURL + @"/oauth/device/token";

    // add to history (mark as watched)
    // used for movies, series, episodes
    // http://docs.trakt.apiary.io/#reference/sync/add-to-history/add-items-to-watched-history?console=1
    public const string SyncHistoryAdd = TraktConstants.BaseAPIURL + @"/sync/history";

    // remove from history (mark as un-watched)
    // used for movies, series, episodes
    // http://docs.trakt.apiary.io/#reference/sync/remove-from-history/remove-items-from-history?console=1
    public const string SyncHistoryRemove = TraktConstants.BaseAPIURL + @"/sync/history/remove";

    // get all the shows and episodes a user has watched
    // http://docs.trakt.apiary.io/#reference/users/history/get-watched-history
    public const string GetWatchedShows = TraktConstants.BaseAPIURL + @"/sync/watched/shows";

    // get all the movies a user has watched
    // http://docs.trakt.apiary.io/#reference/users/history/get-watched-history
    public const string GetWatchedMovies = TraktConstants.BaseAPIURL + @"/sync/watched/movies";

    public const string SetScrobbleStart = TraktConstants.BaseAPIURL + @"/scrobble/start";
    public const string SetScrobblePause = TraktConstants.BaseAPIURL + @"/scrobble/pause";
    public const string SetScrobbleStop = TraktConstants.BaseAPIURL + @"/scrobble/stop";
}
