namespace Shoko.Plugin.Tmdb;

/// <summary>
/// Picks the TMDb API key to send.
/// </summary>
/// <remarks>
/// CI rewrites <see cref="Constants.ApiKey"/> for official builds, so in the
/// tree the comparison below is between two equal literals. It is not once
/// stamped.
/// </remarks>
internal static class TmdbApiKey
{
    /// <summary>
    /// Whether the build was stamped with an API key of its own.
    /// </summary>
    public static bool HasBuiltInApiKey => Constants.ApiKey != "TMDB_API_KEY_GOES_HERE";

    /// <summary>
    /// Resolves the API key, preferring the configured one over the key an
    /// official build was stamped with. An empty configured key is no key.
    /// </summary>
    /// <param name="configuration">The plugin's configuration.</param>
    /// <returns>The key, or <see langword="null"/> when neither is available.</returns>
    public static string? Resolve(TmdbConfiguration configuration)
    {
        if (!string.IsNullOrWhiteSpace(configuration.UserApiKey))
            return configuration.UserApiKey;

        return HasBuiltInApiKey ? Constants.ApiKey : null;
    }
}
