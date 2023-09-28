

using Newtonsoft.Json;

namespace Shoko.Server.Providers.TMDB.Search;

public class TMDBOfflineSearch_Show
{
    /// <summary>
    /// TMDB Movie ID.
    /// </summary>
    [JsonProperty("id")]
    public int ID = 0;

    /// <summary>
    /// Original Title in the movie's native language.
    /// </summary>
    [JsonProperty("original_title")]
    public string Title = string.Empty;

    /// <summary>
    /// Indicates the show is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    public bool IsRestricted = false;

    /// <summary>
    /// Global popularity ranking at the time the dumping took place.
    /// </summary>
    [JsonProperty("popularity")]
    public double Popularity = 0d;
}
