using Newtonsoft.Json;

namespace Shoko.TestData;

/// <summary>
///   An AniDB anime whose description has an early-showing note, with what
///   the prototype the regular air date reader was ported from made of it.
///   Only the note's lines of the description are kept; the prototype gives
///   the same result on them as on the whole.
/// </summary>
public class RegularAirDateCase
{
    /// <summary>
    ///   The AniDB anime ID.
    /// </summary>
    [JsonProperty("id")]
    public int AnimeID { get; set; }

    /// <summary>
    ///   The AniDB anime type, as its number.
    /// </summary>
    [JsonProperty("type")]
    public int Type { get; set; }

    /// <summary>
    ///   The anime's air date, if any, which may be a year and month only.
    /// </summary>
    [JsonProperty("air")]
    public string? AirDate { get; set; }

    /// <summary>
    ///   The anime's air date, when it is a whole date.
    /// </summary>
    public DateOnly? CompleteAirDate
        => DateOnly.TryParseExact(AirDate, "yyyy-MM-dd", out var date) ? date : null;

    /// <summary>
    ///   The note's lines of the description.
    /// </summary>
    [JsonProperty("desc")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    ///   The dated normal episodes: number and stored date.
    /// </summary>
    [JsonProperty("eps")]
    public List<object[]> Episodes { get; set; } = [];

    /// <summary>
    ///   The prototype's status, such as <c>corrected</c> or
    ///   <c>no-regular-date</c>.
    /// </summary>
    [JsonProperty("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    ///   The regular start the prototype worked out, if any.
    /// </summary>
    [JsonProperty("regular")]
    public DateOnly? RegularStart { get; set; }

    /// <summary>
    ///   The moved episodes: number, stored date and regular date.
    /// </summary>
    [JsonProperty("moved")]
    public List<object[]> Moved { get; set; } = [];

    /// <summary>
    ///   The dated normal episodes, typed.
    /// </summary>
    public IEnumerable<(int Number, DateOnly AirDate)> TypedEpisodes
        => Episodes.Select(episode => (Convert.ToInt32(episode[0]), DateOnly.Parse((string)episode[1])));

    /// <summary>
    ///   The moved episodes, typed.
    /// </summary>
    public IEnumerable<(int Number, DateOnly Stored, DateOnly Regular)> TypedMoved
        => Moved.Select(episode => (Convert.ToInt32(episode[0]), DateOnly.Parse((string)episode[1]), DateOnly.Parse((string)episode[2])));
}
