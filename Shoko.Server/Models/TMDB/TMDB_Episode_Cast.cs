
#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Recurring cast member within a season.
/// </summary>
public class TMDB_Episode_Cast
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_Episode_CastID { get; set; }

    /// <summary>
    /// TMDB Show ID for the show this role belongs to.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Show ID for the season this role belongs to.
    /// </summary>
    public int TmdbSeasonID { get; set; }

    /// <summary>
    /// TMDB Episode ID for the episode this role belongs to.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// TMDB Person ID for the cast memeber.
    /// </summary>
    public int TmdbPersonID { get; set; }

    /// <summary>
    /// TMDB Credit ID for the acting job.
    /// </summary>
    public string TmdbCreditID { get; set; } = string.Empty;

    /// <summary>
    /// Character name.
    /// </summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>
    /// Indicates the role is not a recurring role within the season.
    /// </summary>
    public bool IsGuestRole { get; set; }

    /// <summary>
    /// Ordering.
    /// </summary>
    public int Ordering { get; set; }
}
