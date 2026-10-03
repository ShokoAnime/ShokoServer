namespace Shoko.Plugin.Tmdb.Api;

/// <summary>
///   Why the plugin's requests to TMDb are paused.
/// </summary>
public enum TmdbPauseReason
{
    /// <summary>
    ///   They are not.
    /// </summary>
    None = 0,

    /// <summary>
    ///   TMDb answered 429 and asked for a wait.
    /// </summary>
    RateLimited = 1,

    /// <summary>
    ///   TMDb answered with server errors in quick succession.
    /// </summary>
    ServerErrors = 2,
}
