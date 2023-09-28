
#nullable enable
namespace Shoko.Server.Models.TMDB;

public class TMDB_AlternateOrdering_Episode
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrdering_EpisodeID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode Group Season ID.
    /// </summary>
    public string TmdbEpisodeGroupSeasonID { get; set; } = string.Empty;

    /// <summary>
    /// TMDB Episode ID.
    /// </summary>
    public int TmdbEpisodeID { get; set; }

    /// <summary>
    /// Overridden season number for alternate ordering.
    /// </summary>
    public int SeasonNumber { get; set; }

    /// <summary>
    /// Overridden episode number for alternate ordering.
    /// </summary>
    /// <value></value>
    public int EpisodeNumber { get; set; }
}
