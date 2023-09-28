using Shoko.Server.Providers.TMDB;

#nullable enable
namespace Shoko.Server.Models.TMDB;

/// <summary>
/// Alternate Season and Episode ordering using TMDB's "Episode Group" feature.
/// Note: don't ask me why they called it that.
/// </summary>
public class TMDB_AlternateOrdering
{
    /// <summary>
    /// Local ID.
    /// </summary>
    public int TMDB_AlternateOrderingID { get; set; }

    /// <summary>
    /// TMDB Show ID.
    /// </summary>
    public int TmdbShowID { get; set; }

    /// <summary>
    /// TMDB Network ID.
    /// </summary>
    /// <remarks>
    /// It may be null if the group is not tied to a network.
    /// </remarks>
    public int? TmdbNetworkID { get; set; }

    /// <summary>
    /// TMDB Episode Group ID.
    /// </summary>
    public string TmdbEpisodeGroupID { get; set; } = string.Empty;

    /// <summary>
    /// The name of the alternate ordering scheme.
    /// </summary>
    public string EnglishTitle { get; set; } = string.Empty;

    /// <summary>
    /// A short overview about what the scheme entails.
    /// </summary>
    public string EnglishOverview { get; set; } = string.Empty;

    /// <summary>
    /// Number of episodes within the episode group.
    /// </summary>
    public int EpisodeCount { get; set; }

    /// <summary>
    /// Number of seasons within the episode group.
    /// </summary>
    public int SeasonCount { get; set; }

    /// <summary>
    /// 
    /// </summary>
    public AlternateOrderingType Type { get; set; }
}
