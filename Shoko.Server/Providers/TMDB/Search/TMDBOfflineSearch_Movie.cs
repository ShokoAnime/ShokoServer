

using Newtonsoft.Json;

namespace Shoko.Server.Providers.TMDB.Search;

public class TMDBOfflineSearch_Movie
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
    /// Indicates the movie is restricted to an age group above the legal age,
    /// because it's a pornography.
    /// </summary>
    [JsonProperty("adult")]
    public bool IsRestricted = false;

    /// <summary>
    /// Indicates the entry is not truly a movie, including but not limited to
    /// the types:
    ///
    /// - official compilations,
    /// - best of,
    /// - filmed sport events,
    /// - music concerts,
    /// - plays or stand-up show,
    /// - fitness video,
    /// - health video,
    /// - live movie theater events (art, music),
    /// - and how-to DVDs,
    ///
    /// among others.
    /// </summary>
    [JsonProperty("video")]
    public bool IsVideo = false;

    /// <summary>
    /// Global popularity ranking at the time the dumping took place.
    /// </summary>
    [JsonProperty("popularity")]
    public double Popularity = 0d;
}
