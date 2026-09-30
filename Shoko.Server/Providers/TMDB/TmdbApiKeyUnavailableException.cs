using Shoko.Abstractions.Metadata;
using Shoko.Abstractions.Metadata.Providers;

namespace Shoko.Server.Providers.TMDB;

/// <summary>
///   Thrown when TMDB is called without an API key, neither the user's nor
///   one built in.
/// </summary>
/// <remarks>
///   TMDB is unavailable until a key is set, so a search fails the way it
///   does while TMDB is out of reach and writes nothing. The API answers it
///   with <c>503 Service Unavailable</c> and no <c>Retry-After</c>, as waiting
///   does not bring a key.
/// </remarks>
public class TmdbApiKeyUnavailableException() : MetadataProviderNotConfiguredException(MetadataSource.TMDB, Reason)
{
    /// <summary>
    ///   Why TMDB cannot be used.
    /// </summary>
    public const string Reason = "TMDB is not configured: no API key.";
}
