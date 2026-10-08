namespace Shoko.Plugin.Tmdb;

/// <summary>
/// Build-time constants. The value here is a placeholder in the tree and is
/// rewritten by CI for official builds.
/// </summary>
internal static class Constants
{
    /// <summary>
    /// The TMDB API key official builds ship with, substituted by CI from the
    /// <c>TMDB_API</c> secret. For a build from source the placeholder stays,
    /// and the plugin then needs a key from its configuration.
    /// </summary>
    /// <remarks>
    /// The comparison against the placeholder lives in <see cref="TmdbApiKey"/>,
    /// because CI rewrites this file and would rewrite the compared literal too.
    /// </remarks>
    public const string ApiKey = "TMDB_API_KEY_GOES_HERE";
}
